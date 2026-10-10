using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Projecta.Json;
using Projecta.Objects;

namespace Projecta.EntityFrameworkCore;

public sealed record EfCoreReadOptions
{
    public IReadOnlySet<string>? ScalarProperties { get; init; }
    public IReadOnlySet<string> LoadedNavigations { get; init; } = new HashSet<string>(StringComparer.Ordinal);
    public bool OmitUnloadedNavigations { get; init; }
    public bool IncludeShadowProperties { get; init; }
}

/// <summary>
/// Reads tracked entities with lazy loading and DetectChanges disabled for the snapshot.
/// Register this scoped adapter even for non-proxy entities using ILazyLoader. Never share a DbContext concurrently.
/// </summary>
public sealed class EfCoreSourceAdapter : IObjectSourceAdapter
{
    private readonly DbContext _db;
    private readonly EfCoreReadOptions _options;
    private readonly HashSet<string>? _scalars;
    private readonly HashSet<string> _navigations;

    public EfCoreSourceAdapter(DbContext db, EfCoreReadOptions? options = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _options = options ?? new();
        _scalars = _options.ScalarProperties is null ? null : new(_options.ScalarProperties, StringComparer.Ordinal);
        _navigations = new(_options.LoadedNavigations, StringComparer.Ordinal);
    }

    public bool CanRead(object source)
    {
        Type type = source.GetType();
        // FindRuntimeEntityType understands derived proxy types; the fallback recognizes owned/shared CLR types,
        // so they are not accidentally sent through an unrestricted CLR serializer.
        return _db.Model.FindRuntimeEntityType(type) is not null ||
            _db.Model.GetEntityTypes().Any(e => e.ClrType.IsAssignableFrom(type));
    }

    public JToken Read(object source, ObjectReadContext context)
    {
        var tracker = _db.ChangeTracker;
        bool detect = tracker.AutoDetectChangesEnabled, lazy = tracker.LazyLoadingEnabled;
        try
        {
            tracker.AutoDetectChangesEnabled = false;
            tracker.LazyLoadingEnabled = false;
            // Use tracked identity, including owned/shared type entities, without attaching anything.
            EntityEntry? entry = tracker.Entries().FirstOrDefault(e => ReferenceEquals(e.Entity, source));
            if (entry is null || entry.State == EntityState.Detached)
                throw new ObjectReadException("EF source must already be tracked by the configured DbContext.");
            var result = new JObject();
            foreach (var property in entry.Properties)
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                if (!_options.IncludeShadowProperties && property.Metadata.IsShadowProperty()) continue;
                if (_scalars is not null && !_scalars.Contains(property.Metadata.Name)) continue;
                result.Add(property.Metadata.Name, context.Read(property.CurrentValue));
            }
            foreach (var complex in entry.ComplexProperties)
            {
                if (_scalars is null || _scalars.Contains(complex.Metadata.Name))
                    result.Add(complex.Metadata.Name, ReadComplex(complex, context));
            }
            foreach (var collection in entry.ComplexCollections)
            {
                if (_scalars is null || _scalars.Contains(collection.Metadata.Name))
                    result.Add(collection.Metadata.Name, context.Read(collection.CurrentValue));
            }
            foreach (var navigation in entry.Navigations)
            {
                if (!_navigations.Contains(navigation.Metadata.Name)) continue;
                if (!navigation.IsLoaded)
                {
                    if (_options.OmitUnloadedNavigations) continue;
                    throw new ObjectReadException($"Requested navigation '{navigation.Metadata.Name}' is not loaded.");
                }
                result.Add(navigation.Metadata.Name, context.Read(navigation.CurrentValue));
            }
            return result;
        }
        finally { tracker.LazyLoadingEnabled = lazy; tracker.AutoDetectChangesEnabled = detect; }
    }

    private static JToken ReadComplex(ComplexPropertyEntry entry, ObjectReadContext context)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (entry.CurrentValue is null) return JValue.CreateNull();
        var result = new JObject();
        foreach (var property in entry.Properties) result.Add(property.Metadata.Name, context.Read(property.CurrentValue));
        foreach (var child in entry.ComplexProperties) result.Add(child.Metadata.Name, ReadComplex(child, context));
        foreach (var collection in entry.ComplexCollections) result.Add(collection.Metadata.Name, context.Read(collection.CurrentValue));
        return result;
    }
}
