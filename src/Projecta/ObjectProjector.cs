using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Projecta.Json;
using Projecta.Objects;

namespace Projecta.Mapping;

public interface IProjectionTargetMaterializer
{
    bool CanMaterialize(Type targetType);
    object? Materialize(JToken value, Type targetType);
}

public sealed partial class ObjectProjector
{
    private readonly ProjectionOptions _options;
    public ProjectionOptions Options => _options;
    private readonly ObjectReader _reader;
    private readonly IProjectionTargetMaterializer[] _materializers;
    private readonly JsonSerializerOptions _json;
    private readonly SemaphoreSlim _compilers;
    private readonly object _cacheLock = new();
    private readonly Dictionary<string, LinkedListNode<(string Key, ProjectaQuery Query)>> _cache = new(StringComparer.Ordinal);
    private readonly LinkedList<(string Key, ProjectaQuery Query)> _lru = new();
    private long _hits, _misses, _evictions;
    public int CachedExpressionCount { get { lock (_cacheLock) return _cache.Count; } }
    public ProjectionCacheStatistics CacheStatistics
    { get { lock (_cacheLock) return new(_hits, _misses, _evictions, _cache.Count, _options.CacheCapacity); } }

    // The original public constructor signature is retained.
    public ObjectProjector(ProjectionOptions? options = null,
        IEnumerable<IObjectSourceAdapter>? sourceAdapters = null,
        IEnumerable<IProjectionTargetMaterializer>? targetMaterializers = null)
    {
        var supplied = options ?? new ProjectionOptions();
        // Freeze caller-owned collections so reader, preflight and replay metadata see identical policies.
        _options = supplied with
        {
            Source = Snapshot(supplied.Source),
            Output = Snapshot(supplied.Output),
            Bindings = Snapshot(supplied.Bindings)
        };
        if (_options.CacheCapacity < 1 || _options.MaxConcurrentCompilations is < 1 or > 128 || !Enum.IsDefined(_options.NullProperties))
            throw new ArgumentOutOfRangeException(nameof(options));
        _reader = new ObjectReader(_options.Source, sourceAdapters);
        _ = new ObjectReader(_options.Output);
        _ = new ObjectReader(_options.Bindings);
        _materializers = targetMaterializers?.ToArray() ?? Array.Empty<IProjectionTargetMaterializer>();
        if (_materializers.Any(m => m is null)) throw new ArgumentException("Null target materializer.");
        _compilers = new SemaphoreSlim(_options.MaxConcurrentCompilations, _options.MaxConcurrentCompilations);
        _json = new JsonSerializerOptions
        {
            MaxDepth = _options.Output.MaxDepth,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            PropertyNameCaseInsensitive = false
        };
        _json.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        _json.MakeReadOnly(populateMissingResolver: true);
    }

    private static ObjectReadOptions Snapshot(ObjectReadOptions source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source with
        {
            AllowedRootMembers = source.AllowedRootMembers is null ? null :
                Array.AsReadOnly(source.AllowedRootMembers.Distinct(StringComparer.Ordinal)
                    .OrderBy(x => x, StringComparer.Ordinal).ToArray())
        };
    }

    public CompiledProjection Compile(ProjectionDefinition definition) => Compile(definition, default);

    public CompiledProjection Compile(ProjectionDefinition definition, CancellationToken cancellationToken)
    {
        long compilationStarted = Stopwatch.GetTimestamp();
        try
        {
        ArgumentNullException.ThrowIfNull(definition);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = definition with
        {
            Fields = definition.Fields?.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal),
            InputSchema = definition.InputSchema?.Clone(), OutputSchema = definition.OutputSchema?.Clone()
        };
        string expression = snapshot.BuildExpression();
        if (expression.Length > 32_768) throw new ProjectionConfigurationException("Expression exceeds 32768 characters.");
        if ((snapshot.InputSchema.HasValue || snapshot.OutputSchema.HasValue) && _options.ContractProvider is null)
            throw new ProjectionConfigurationException("Schema assertions require an explicit contract provider.");
        ProjectaQuery? query;
        lock (_cacheLock)
        {
            query = FindCached(expression);
            if (query is not null) _hits++; else _misses++;
        }
        if (query is null)
        {
            // Parsing never holds the LRU lock; concurrent cold compilation is independently bounded.
            _compilers.Wait(cancellationToken);
            try
            {
                lock (_cacheLock) query = FindCached(expression);
                if (query is null)
                {
                    try { query = new ProjectaQuery(expression); }
                    catch (Exception ex) when (ProjectionErrors.CanReport(ex))
                    { throw new ProjectionConfigurationException("Invalid projection expression.", ex); }
                    lock (_cacheLock)
                    {
                        var existing = FindCached(expression);
                        if (existing is not null) query = existing;
                        else
                        {
                            if (_cache.Count == _options.CacheCapacity)
                            { var oldest = _lru.Last!; _cache.Remove(oldest.Value.Key); _lru.RemoveLast(); _evictions++; }
                            _cache.Add(expression, _lru.AddFirst((expression, query)));
                        }
                    }
                }
            }
            finally { _compilers.Release(); }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new CompiledProjection(snapshot, query,
            snapshot.InputSchema is { } input ? _options.ContractProvider!.Compile(input) : null,
            snapshot.OutputSchema is { } output ? _options.ContractProvider!.Compile(output) : null);
        }
        finally { ProjectionTelemetry.RecordStage("compile", compilationStarted); }
    }

    private ProjectaQuery? FindCached(string expression)
    {
        if (!_cache.TryGetValue(expression, out var node)) return null;
        _lru.Remove(node); _lru.AddFirst(node); return node.Value.Query;
    }

    public ProjectionResult<CompiledProjection> TryCompile(ProjectionDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        try { return ProjectionResult<CompiledProjection>.Succeeded(Compile(definition, cancellationToken)); }
        catch (Exception ex) when (ProjectionErrors.CanReport(ex))
        {
            var diagnostics = ProjectionErrors.Describe(ex, ProjectionPhase.Compile, definition.Id, definition.Version)
                .Select(d => d with { Field = CompiledProjection.FindField(definition, d.Position) });
            return ProjectionResult<CompiledProjection>.Failed(diagnostics);
        }
    }
}
