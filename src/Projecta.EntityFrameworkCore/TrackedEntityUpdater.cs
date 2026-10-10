using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.SystemTextJson;

namespace Projecta.EntityFrameworkCore;

/// <summary>
/// Explicit scalar patch only. Caller owns authorization, domain invariants, transactions and SaveChanges.
/// Does not attach entities, change keys/FKs, merge relationships, or guarantee setter rollback.
/// </summary>
public sealed class TrackedEntityUpdater
{
    private readonly DbContext _db;
    public TrackedEntityUpdater(DbContext db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    public void Apply(object entity, JObject projection, IEnumerable<string> permittedProperties)
    {
        ArgumentNullException.ThrowIfNull(entity); ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(permittedProperties);
        var permitted = new HashSet<string>(permittedProperties, StringComparer.Ordinal);
        var tracker = _db.ChangeTracker;
        bool detect = tracker.AutoDetectChangesEnabled, lazy = tracker.LazyLoadingEnabled;
        try
        {
            tracker.AutoDetectChangesEnabled = false; tracker.LazyLoadingEnabled = false;
            var entry = tracker.Entries().FirstOrDefault(e => ReferenceEquals(e.Entity, entity));
            if (entry is null || entry.State is EntityState.Detached or EntityState.Deleted)
                throw new ProjectionMaterializationException("Target must be tracked and not deleted.");
            var pending = new List<(PropertyEntry Property, object? Value)>();
            var options = new JsonSerializerOptions { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
            options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
            foreach (var pair in projection.Properties)
            {
                if (!permitted.Contains(pair.Key)) throw new ProjectionMaterializationException("Target property is not permitted.");
                var property = entry.Properties.FirstOrDefault(p => p.Metadata.Name == pair.Key);
                if (property is null || property.Metadata.IsShadowProperty() || property.Metadata.IsPrimaryKey() ||
                    property.Metadata.GetContainingForeignKeys().Any() || property.Metadata.IsConcurrencyToken ||
                    property.Metadata.ValueGenerated != ValueGenerated.Never ||
                    property.Metadata.GetAfterSaveBehavior() != PropertySaveBehavior.Save)
                    throw new ProjectionMaterializationException("Key, foreign key, shadow, concurrency, generated or non-scalar target is forbidden.");
                if (pair.Value.Type == JTokenType.Null && !property.Metadata.IsNullable)
                    throw new ProjectionMaterializationException("Null value is not allowed by the entity model.");
                var node = pair.Value.ToSystemTextJsonNode();
                object? value = node is null ? null : node.Deserialize(property.Metadata.ClrType, options);
                pending.Add((property, value));
            }
            // All contracts/conversions are validated first. Custom throwing setters can still leave partial state.
            foreach (var assignment in pending) assignment.Property.CurrentValue = assignment.Value;
        }
        finally { tracker.LazyLoadingEnabled = lazy; tracker.AutoDetectChangesEnabled = detect; }
    }
}
