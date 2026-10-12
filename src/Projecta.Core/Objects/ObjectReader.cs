using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Projecta.Json;

namespace Projecta.Objects;

public sealed class ObjectReader
{
    internal static readonly ObjectReader Default = new();
    private readonly ObjectReadOptions _options;
    private readonly IObjectSourceAdapter[] _adapters;
    private readonly IReadOnlySet<string>? _exposure;

    public ObjectReader(ObjectReadOptions? options = null, IEnumerable<IObjectSourceAdapter>? adapters = null)
    {
        _options = options ?? new ObjectReadOptions();
        _options.Validate();
        _exposure = _options.AllowedRootMembers is null ? null : new HashSet<string>(_options.AllowedRootMembers, StringComparer.Ordinal);
        _adapters = adapters?.ToArray() ?? Array.Empty<IObjectSourceAdapter>();
        if (_adapters.Any(a => a is null)) throw new ArgumentException("Null source adapter.", nameof(adapters));
    }

    public JToken Read(object? source, CancellationToken cancellationToken = default)
    {
        var context = new ObjectReadContext(_options, _adapters, cancellationToken, allowedRootMembers: _exposure);
        JToken result = context.Read(source);
        if (_adapters.Length != 0) TokenValidation.Validate(result, _options, cancellationToken);
        return result;
    }
    /// <summary>Selects literal source member paths for POCOs, without changing dictionary/JSON DOM semantics.</summary>
    public JToken ReadSelectedPaths(object? source, IEnumerable<IReadOnlyList<string>> paths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var context = new ObjectReadContext(_options, _adapters, cancellationToken,
            paths: paths, allowedRootMembers: _exposure);
        JToken result = context.Read(source);
        if (_adapters.Length != 0) TokenValidation.Validate(result, _options, cancellationToken);
        return result;
    }

    public JToken ReadSelected(object? source, IReadOnlySet<string> rootMembers, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rootMembers);
        return ReadSelectedPaths(source, rootMembers.Select(x => (IReadOnlyList<string>)new[] { x }), cancellationToken);
    }

}

public sealed class ObjectReadContext
{
    private sealed record Member(PropertyInfo Property, Func<object, object?> Get, string JsonName,
        JsonIgnoreCondition? Ignore);
    private sealed class Plan { public Member[] Members { get; } public Plan(Member[] members) => Members = members; }
    private static readonly ConditionalWeakTable<Type, Plan> Plans = new();
    private readonly ObjectReadOptions _options;
    private readonly IObjectSourceAdapter[] _adapters;
    private readonly CancellationToken _cancellation;
    private readonly HashSet<object> _active = new(ReferenceEqualityComparer.Instance);
    private readonly SelectionNode? _rootSelection;
    private readonly IReadOnlySet<string>? _allowedRootMembers;

    private sealed class SelectionNode
    {
        public bool Full { get; set; }
        public Dictionary<string, SelectionNode> Children { get; } = new(StringComparer.Ordinal);
        public static SelectionNode Build(IEnumerable<IReadOnlyList<string>> paths)
        {
            var root = new SelectionNode();
            int pathCount = 0;
            foreach (IReadOnlyList<string> path in paths)
            {
                if (++pathCount > 512 || path.Count is < 1 or > 128)
                    throw new ObjectReadException("Selected source path limit exceeded.");
                var node = root;
                foreach (string segment in path)
                {
                    if (string.IsNullOrEmpty(segment) || segment.Length > 256)
                        throw new ObjectReadException("Invalid selected member path.");
                    if (!node.Children.TryGetValue(segment, out var child))
                        node.Children.Add(segment, child = new SelectionNode());
                    node = child;
                }
                node.Full = true;
            }
            return root;
        }
    }

    private readonly Dictionary<object, object> _state = new(ReferenceEqualityComparer.Instance);
    private int _depth;
    private int _nodes;
    private long _characters;

    internal ObjectReadContext(ObjectReadOptions options, IObjectSourceAdapter[] adapters, CancellationToken cancellation,
        IEnumerable<IReadOnlyList<string>>? paths = null, IReadOnlySet<string>? allowedRootMembers = null)
    {
        _options = options; _adapters = adapters; _cancellation = cancellation;
        _rootSelection = paths is null || adapters.Length != 0 ? null : SelectionNode.Build(paths);
        _allowedRootMembers = allowedRootMembers;
    }

    /// <summary>Per-snapshot adapter state; discarded when the root read completes.</summary>
    public T GetOrCreateState<T>(object owner, Func<T> factory) where T : class
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(factory);
        if (_state.TryGetValue(owner, out object? state)) return (T)state;
        T created = factory() ?? throw new InvalidOperationException("Adapter state cannot be null.");
        _state.Add(owner, created);
        return created;
    }

    public CancellationToken CancellationToken => _cancellation;

    public JToken Read(object? source) => ReadCore(source, _rootSelection, exposureRoot: true);

    private JToken ReadCore(object? source, SelectionNode? selection, bool exposureRoot = false)
    {
        _cancellation.ThrowIfCancellationRequested();
        if (++_nodes > _options.MaxNodes) throw new ObjectReadException("Source node limit exceeded.");
        if (++_depth > _options.MaxDepth) { --_depth; throw new ObjectReadException("Source depth limit exceeded."); }
        bool tracked = false;
        try
        {
            if (source is null) return JValue.CreateNull();
            switch (source)
            {
                case string s: ChargeText(s); return new JValue(s);
                case char c: return new JValue(c);
                case bool b: return new JValue(b);
                case byte n: return new JValue(n);
                case sbyte n: return new JValue(n);
                case short n: return new JValue(n);
                case ushort n: return new JValue(n);
                case int n: return new JValue(n);
                case uint n: return new JValue((long)n);
                case long n: return new JValue(n);
                case ulong n: return n <= long.MaxValue ? new JValue((long)n) : new JValue((decimal)n);
                case float n: if (!float.IsFinite(n)) throw new ObjectReadException("Non-finite number."); return new JValue(n);
                case double n: if (!double.IsFinite(n)) throw new ObjectReadException("Non-finite number."); return new JValue(n);
                case decimal n: return new JValue(n);
                case Guid g: return new JValue(g.ToString("D"));
                case DateTime d: return new JValue(d);
                case DateTimeOffset d: return new JValue(d);
                case DateOnly d: return new JValue(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                case TimeOnly d: return new JValue(d.ToString("O", CultureInfo.InvariantCulture));
                case TimeSpan d: return new JValue(d);
                case Enum e: return new JValue(e.ToString());
                case JsonElement e: return ReadElement(e, exposureRoot);
                case JsonDocument d: return ReadElement(d.RootElement, exposureRoot);
            }
            if (!source.GetType().IsValueType)
            {
                if (!_active.Add(source)) throw new ObjectReadException("Circular source reference detected.");
                tracked = true;
            }
            foreach (IObjectSourceAdapter adapter in _adapters)
                if (adapter.CanRead(source))
                {
                    JToken adapted = adapter.Read(source, this)
                        ?? throw new ObjectReadException("Source adapter returned no token.");
                    return FilterRoot(adapted, exposureRoot);
                }
            if (source is JToken token) return ReadToken(token, exposureRoot);
            if (source is JsonNode node) return ReadNode(node, exposureRoot);
            Type type = source.GetType();
            if (type.Assembly.IsDynamic || type.GetInterfaces().Any(i => i.FullName == "Castle.DynamicProxy.IProxyTargetAccessor"))
                throw new ObjectReadException("Proxy source requires an explicit adapter.");
            if (source is IQueryable || source is Delegate || source is Task || source is Stream || source is MemberInfo)
                throw new ObjectReadException("Executable, queryable or resource-backed source is not a data object.");
            if (source is IDictionary dictionary)
            {
                var result = new JObject();
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Key is not string key) throw new ObjectReadException("Dictionary keys must be strings.");
                    if (!AllowsRoot(key, exposureRoot)) continue;
                    ChargeText(key); result.Add(key, ReadCore(entry.Value, null));
                }
                return result;
            }
            Type? map = type.GetInterfaces().FirstOrDefault(i => i.IsGenericType &&
                (i.GetGenericTypeDefinition() == typeof(IDictionary<,>) || i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));
            if (map is not null && source is IEnumerable entries)
            {
                if (map.GetGenericArguments()[0] != typeof(string)) throw new ObjectReadException("Dictionary keys must be strings.");
                var result = new JObject();
                foreach (object entry in entries)
                {
                    _cancellation.ThrowIfCancellationRequested();
                    var plan = Plans.GetValue(entry.GetType(), CreatePlan);
                    var key = (string)plan.Members.Single(m => m.Property.Name == "Key").Get(entry)!;
                    if (!AllowsRoot(key, exposureRoot)) continue;
                    ChargeText(key); result.Add(key, ReadCore(plan.Members.Single(m => m.Property.Name == "Value").Get(entry), null));
                }
                return result;
            }
            if (source is IEnumerable sequence)
            {
                var array = new JArray();
                foreach (object? item in sequence) array.Add(ReadCore(item, selection, exposureRoot));
                return array;
            }
            var obj = new JObject();
            foreach (var member in Plans.GetValue(type, CreatePlan).Members)
            {
                _cancellation.ThrowIfCancellationRequested();
                if (member.Ignore == JsonIgnoreCondition.Always || _options.IncludeProperty?.Invoke(type, member.Property) == false) continue;
                string name = _options.UseJsonPropertyNames ? member.JsonName : member.Property.Name;
                if (!AllowsRoot(name, exposureRoot)) continue;
                SelectionNode? next = null;
                if (selection is not null && !selection.Children.TryGetValue(name, out next)) continue;
                ChargeText(name);
                object? value;
                try { value = member.Get(source); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { throw new ObjectReadException($"Source getter '{member.Property.Name}' failed.", ex); }
                if (member.Ignore == JsonIgnoreCondition.WhenWritingNull && value is null) continue;
                if (member.Ignore == JsonIgnoreCondition.WhenWritingDefault &&
                    (value is null || (member.Property.PropertyType.IsValueType && Equals(value, Activator.CreateInstance(member.Property.PropertyType))))) continue;
                obj.Add(name, ReadCore(value, next is { Full: true } ? null : next));
            }
            return obj;
        }
        finally
        {
            if (tracked) _active.Remove(source!);
            --_depth;
        }
    }

    private bool AllowsRoot(string name, bool exposureRoot) => !exposureRoot || _allowedRootMembers is null || _allowedRootMembers.Contains(name);

    private JToken FilterRoot(JToken result, bool exposureRoot)
    {
        if (!exposureRoot || _allowedRootMembers is null || result is not JObject obj) return result;
        var exposed = new JObject();
        foreach (var p in obj.Properties) if (_allowedRootMembers.Contains(p.Key)) exposed.Add(p.Key, p.Value.DeepClone());
        return exposed;
    }

    private void ChargeText(string text)
    {
        _characters += text.Length;
        if (_characters > _options.MaxCharacters) throw new ObjectReadException("Source character limit exceeded.");
        _cancellation.ThrowIfCancellationRequested();
    }

    private JToken ReadToken(JToken token, bool exposureRoot)
    {
        switch (token)
        {
            case JObject o:
                var obj = new JObject();
                foreach (var p in o.Properties) { if (!AllowsRoot(p.Key, exposureRoot)) continue; ChargeText(p.Key); obj.Add(p.Key, ReadCore(p.Value, null)); }
                return obj;
            case JArray a:
                var arr = new JArray(); foreach (var item in a.ChildrenTokens) arr.Add(ReadCore(item, null, exposureRoot)); return arr;
            case JValue v:
                if (v.Type == JTokenType.String) ChargeText((string)v);
                return v.DeepClone();
            default: throw new ObjectReadException("Function tokens cannot be used as source data.");
        }
    }

    private JToken ReadElement(JsonElement e, bool exposureRoot)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new JObject(); foreach (var p in e.EnumerateObject()) { if (!AllowsRoot(p.Name, exposureRoot)) continue; ChargeText(p.Name); obj.Add(p.Name, ReadCore(p.Value, null)); } return obj;
            case JsonValueKind.Array:
                var arr = new JArray(); foreach (var c in e.EnumerateArray()) arr.Add(ReadCore(c, null, exposureRoot)); return arr;
            case JsonValueKind.String: string s = e.GetString()!; ChargeText(s); return new JValue(s);
            case JsonValueKind.Number: return e.TryGetInt64(out long l) ? new JValue(l) : e.TryGetDecimal(out decimal d) && (double)d == e.GetDouble() ? new JValue(d) : new JValue(e.GetDouble());
            case JsonValueKind.True: return new JValue(true);
            case JsonValueKind.False: return new JValue(false);
            case JsonValueKind.Null: return JValue.CreateNull();
            default: throw new ObjectReadException("Undefined JSON element.");
        }
    }

    private JToken ReadNode(JsonNode node, bool exposureRoot)
    {
        if (node is JsonObject o)
        { var result = new JObject(); foreach (var p in o) { if (!AllowsRoot(p.Key, exposureRoot)) continue; ChargeText(p.Key); result.Add(p.Key, ReadCore(p.Value, null)); } return result; }
        if (node is JsonArray a)
        { var result = new JArray(); foreach (var item in a) result.Add(ReadCore(item, null, exposureRoot)); return result; }
        // Scalars are bounded and cannot contain arbitrary application getters.
        using JsonDocument doc = JsonDocument.Parse(node.ToJsonString());
        return ReadElement(doc.RootElement, exposureRoot);
    }

    private static Plan CreatePlan(Type type)
    {
        var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetMethod is { IsPublic: true, IsStatic: false } && p.GetIndexParameters().Length == 0)
            .Select(p => new Member(p, Getter(p), p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name,
                p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition)).ToArray();
        return new Plan(members);
    }

    private static Func<object, object?> Getter(PropertyInfo property)
    {
        if (!RuntimeFeature.IsDynamicCodeSupported || property.DeclaringType?.IsVisible != true)
            return source => property.GetValue(source);
        var source = Expression.Parameter(typeof(object), "source");
        var access = Expression.Property(Expression.Convert(source, property.DeclaringType!), property);
        return Expression.Lambda<Func<object, object?>>(Expression.Convert(access, typeof(object)), source).Compile();
    }
}
