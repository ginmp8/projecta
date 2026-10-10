using System;
using System.Collections.Concurrent;

namespace Projecta.Mapping;

public sealed class ProjectionTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _types = new(StringComparer.Ordinal);
    public ProjectionTypeRegistry Register<T>(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        if (!_types.TryAdd(alias, typeof(T))) throw new ProjectionConfigurationException("Target alias already registered.");
        return this;
    }
    public Type Resolve(string alias) => _types.TryGetValue(alias, out Type? type) ? type :
        throw new ProjectionConfigurationException("Target alias is not registered.");
}
