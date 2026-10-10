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

    public ObjectReader(ObjectReadOptions? options = null, IEnumerable<IObjectSourceAdapter>? adapters = null)
    {
        _options = options ?? new ObjectReadOptions();
        _options.Validate();
        _adapters = adapters?.ToArray() ?? Array.Empty<IObjectSourceAdapter>();
        if (_adapters.Any(a => a is null)) throw new ArgumentException("Null source adapter.", nameof(adapters));
    }

    public JToken Read(object? source, CancellationToken cancellationToken = default)
    {
        var context = new ObjectReadContext(_options, _adapters, cancellationToken);
        JToken result = context.Read(source);
        if (_adapters.Length != 0) TokenValidation.Validate(result, _options, cancellationToken);
        return result;
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
    private int _depth;
    private int _nodes;
    private long _characters;

    internal ObjectReadContext(ObjectReadOptions options, IObjectSourceAdapter[] adapters, CancellationToken cancellation)
    { _options = options; _adapters = adapters; _cancellation = cancellation; }

    public CancellationToken CancellationToken => _cancellation;

    public JToken Read(object? source)
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
                case JsonElement e: return ReadElement(e);
                case JsonDocument d: return ReadElement(d.RootElement);
            }
            if (!source.GetType().IsValueType)
            {
                if (!_active.Add(source)) throw new ObjectReadException("Circular source reference detected.");
                tracked = true;
            }
            foreach (IObjectSourceAdapter adapter in _adapters)
                if (adapter.CanRead(source)) return adapter.Read(source, this)
                    ?? throw new ObjectReadException("Source adapter returned no token.");
            if (source is JToken token) return ReadToken(token);
            if (source is JsonNode node) return ReadNode(node);
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
                    ChargeText(key); result.Add(key, Read(entry.Value));
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
                    ChargeText(key); result.Add(key, Read(plan.Members.Single(m => m.Property.Name == "Value").Get(entry)));
                }
                return result;
            }
            if (source is IEnumerable sequence)
            {
                var array = new JArray();
                foreach (object? item in sequence) array.Add(Read(item));
                return array;
            }
            var obj = new JObject();
            foreach (var member in Plans.GetValue(type, CreatePlan).Members)
            {
                _cancellation.ThrowIfCancellationRequested();
                if (member.Ignore == JsonIgnoreCondition.Always || _options.IncludeProperty?.Invoke(type, member.Property) == false) continue;
                string name = _options.UseJsonPropertyNames ? member.JsonName : member.Property.Name;
                ChargeText(name);
                object? value;
                try { value = member.Get(source); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                { throw new ObjectReadException($"Source getter '{member.Property.Name}' failed.", ex); }
                if (member.Ignore == JsonIgnoreCondition.WhenWritingNull && value is null) continue;
                if (member.Ignore == JsonIgnoreCondition.WhenWritingDefault &&
                    (value is null || (member.Property.PropertyType.IsValueType && Equals(value, Activator.CreateInstance(member.Property.PropertyType))))) continue;
                obj.Add(name, Read(value));
            }
            return obj;
        }
        finally
        {
            if (tracked) _active.Remove(source!);
            --_depth;
        }
    }

    private void ChargeText(string text)
    {
        _characters += text.Length;
        if (_characters > _options.MaxCharacters) throw new ObjectReadException("Source character limit exceeded.");
        _cancellation.ThrowIfCancellationRequested();
    }

    private JToken ReadToken(JToken token)
    {
        switch (token)
        {
            case JObject o:
                var obj = new JObject();
                foreach (var p in o.Properties) { ChargeText(p.Key); obj.Add(p.Key, Read(p.Value)); }
                return obj;
            case JArray a:
                var arr = new JArray(); foreach (var item in a.ChildrenTokens) arr.Add(Read(item)); return arr;
            case JValue v:
                if (v.Type == JTokenType.String) ChargeText((string)v);
                return v.DeepClone();
            default: throw new ObjectReadException("Function tokens cannot be used as source data.");
        }
    }

    private JToken ReadElement(JsonElement e)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new JObject(); foreach (var p in e.EnumerateObject()) { ChargeText(p.Name); obj.Add(p.Name, Read(p.Value)); } return obj;
            case JsonValueKind.Array:
                var arr = new JArray(); foreach (var c in e.EnumerateArray()) arr.Add(Read(c)); return arr;
            case JsonValueKind.String: string s = e.GetString()!; ChargeText(s); return new JValue(s);
            case JsonValueKind.Number: return e.TryGetInt64(out long l) ? new JValue(l) : e.TryGetDecimal(out decimal d) ? new JValue(d) : new JValue(e.GetDouble());
            case JsonValueKind.True: return new JValue(true);
            case JsonValueKind.False: return new JValue(false);
            case JsonValueKind.Null: return JValue.CreateNull();
            default: throw new ObjectReadException("Undefined JSON element.");
        }
    }

    private JToken ReadNode(JsonNode node)
    {
        if (node is JsonObject o)
        { var result = new JObject(); foreach (var p in o) { ChargeText(p.Key); result.Add(p.Key, Read(p.Value)); } return result; }
        if (node is JsonArray a)
        { var result = new JArray(); foreach (var item in a) result.Add(Read(item)); return result; }
        // Scalars are bounded and cannot contain arbitrary application getters.
        using JsonDocument doc = JsonDocument.Parse(node.ToJsonString());
        return ReadElement(doc.RootElement);
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
