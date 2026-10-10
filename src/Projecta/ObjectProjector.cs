using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using Projecta.Json;
using Projecta.Objects;
using Projecta.SystemTextJson;

namespace Projecta.Mapping;

public interface IProjectionTargetMaterializer
{
    bool CanMaterialize(Type targetType);
    object? Materialize(JToken value, Type targetType);
}

/// <summary>A frozen query compiled from a configuration snapshot; reusable concurrently without scoped adapters.</summary>
public sealed class CompiledProjection
{
    internal ProjectaQuery Query { get; }
    public string Id { get; }
    public int Version { get; }
    public string? TargetType { get; }
    internal CompiledProjection(ProjectionDefinition definition, ProjectaQuery query)
    { Id = definition.Id; Version = definition.Version; TargetType = definition.TargetType; Query = query; }
}

public sealed class ObjectProjector
{
    private readonly ProjectionOptions _options;
    private readonly ObjectReader _reader;
    private readonly IProjectionTargetMaterializer[] _materializers;
    private readonly JsonSerializerOptions _json;
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, ProjectaQuery Query)>> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, ProjectaQuery Query)> _lru = new();
    public int CachedExpressionCount { get { lock (_cacheLock) return _cache.Count; } }

    public ObjectProjector(ProjectionOptions? options = null,
        IEnumerable<IObjectSourceAdapter>? sourceAdapters = null,
        IEnumerable<IProjectionTargetMaterializer>? targetMaterializers = null)
    {
        _options = options ?? new ProjectionOptions();
        if (_options.CacheCapacity < 1) throw new ArgumentOutOfRangeException(nameof(options));
        _reader = new ObjectReader(_options.Source, sourceAdapters);
        _materializers = targetMaterializers?.ToArray() ?? Array.Empty<IProjectionTargetMaterializer>();
        if (_materializers.Any(m => m is null)) throw new ArgumentException("Null target materializer.");
        _json = new JsonSerializerOptions
        {
            MaxDepth = _options.Output.MaxDepth,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            PropertyNameCaseInsensitive = false
        };
        _json.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        _json.MakeReadOnly(populateMissingResolver: true);
    }

    public CompiledProjection Compile(ProjectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        string expression = definition.BuildExpression();
        if (expression.Length > 32_768) throw new ProjectionConfigurationException("Expression exceeds 32768 characters.");
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(expression, out var existing))
            { _lru.Remove(existing); _lru.AddFirst(existing); return new CompiledProjection(definition, existing.Value.Query); }
            ProjectaQuery query;
            try { query = new ProjectaQuery(expression); }
            catch (Exception ex) { throw new ProjectionConfigurationException("Invalid projection expression.", ex); }
            if (_cache.Count == _options.CacheCapacity)
            { var oldest = _lru.Last!; _cache.Remove(oldest.Value.Key); _lru.RemoveLast(); }
            var node = _lru.AddFirst((expression, query)); _cache.Add(expression, node);
            return new CompiledProjection(definition, query);
        }
    }

    public JToken ProjectToToken(object? source, ProjectionDefinition definition, CancellationToken cancellationToken = default)
        => ProjectToToken(source, Compile(definition), cancellationToken);

    public JToken ProjectToToken(object? source, CompiledProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        JToken input = _reader.Read(source, cancellationToken);
        JToken result = projection.Query.Eval(input, _options.Evaluation, cancellationToken);
        if (result.Type == JTokenType.Undefined) throw new ProjectionMaterializationException("Projection returned undefined; provide a fallback in the rule.");
        TokenValidation.Validate(result, _options.Output, cancellationToken);
        // Detach the result from native mutable input/engine structures.
        return result.DeepClone();
    }

    public T? Project<T>(object? source, ProjectionDefinition definition, CancellationToken cancellationToken = default)
        => (T?)Project(source, typeof(T), Compile(definition), cancellationToken);
    public T? Project<T>(object? source, CompiledProjection projection, CancellationToken cancellationToken = default)
        => (T?)Project(source, typeof(T), projection, cancellationToken);

    public object? Project(object? source, Type targetType, ProjectionDefinition definition, CancellationToken cancellationToken = default)
        => Project(source, targetType, Compile(definition), cancellationToken);

    public object? Project(object? source, ProjectionDefinition definition, ProjectionTypeRegistry registry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (string.IsNullOrWhiteSpace(definition.TargetType)) throw new ProjectionConfigurationException("A registered target alias is required.");
        return Project(source, registry.Resolve(definition.TargetType), Compile(definition), cancellationToken);
    }

    public object? Project(object? source, Type targetType, CompiledProjection projection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        if (targetType.Assembly.IsDynamic || targetType.ContainsGenericParameters)
            throw new ProjectionConfigurationException("Dynamic or open generic target is not supported.");
        JToken result = ProjectToToken(source, projection, cancellationToken);
        foreach (var materializer in _materializers)
            if (materializer.CanMaterialize(targetType)) return materializer.Materialize(result, targetType);
        if (typeof(JToken).IsAssignableFrom(targetType))
            return targetType.IsInstanceOfType(result) ? result : throw new ProjectionMaterializationException("Unexpected native JSON shape.");
        JsonNode? node = result.ToSystemTextJsonNode();
        if (typeof(JsonNode).IsAssignableFrom(targetType))
            return node is null || targetType.IsInstanceOfType(node) ? node : throw new ProjectionMaterializationException("Unexpected JSON node shape.");
        if (targetType == typeof(JsonDocument)) return result.ToSystemTextJson();
        if (targetType == typeof(JsonElement)) { using var doc = result.ToSystemTextJson(); return doc.RootElement.Clone(); }
        try
        {
            object? value = node is null ? JsonSerializer.Deserialize("null", targetType, _json) : node.Deserialize(targetType, _json);
            cancellationToken.ThrowIfCancellationRequested();
            return value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { throw new ProjectionMaterializationException("Projected data is incompatible with the requested target contract.", ex); }
    }
}
