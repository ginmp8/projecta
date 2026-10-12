using System;
using System.Collections.Generic;
using System.Linq;

namespace Projecta.Mapping;

/// <summary>A thread-safe, in-memory rule catalog. Compiles before publication and swaps active plans atomically.</summary>
public sealed class ProjectionRuleRegistry
{
    private sealed class RuleVersions
    {
        internal SortedDictionary<int, CompiledProjection> Plans { get; } = new();
        internal int? ActiveVersion { get; set; }
    }
    private readonly ObjectProjector _projector;
    private readonly int _maxRules;
    private readonly int _maxVersionsPerRule;
    private readonly object _gate = new();
    private readonly Dictionary<string, RuleVersions> _rules = new(StringComparer.Ordinal);

    public ProjectionRuleRegistry(ObjectProjector projector, int maxRules = 1024, int maxVersionsPerRule = 64)
    {
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
        if (maxRules is < 1 or > 100_000 || maxVersionsPerRule is < 1 or > 10_000)
            throw new ArgumentOutOfRangeException(nameof(maxRules));
        _maxRules = maxRules;
        _maxVersionsPerRule = maxVersionsPerRule;
    }

    /// <summary>Successful compilation alone does not activate a rule. Re-publishing different bytes at the same ID/version is forbidden.</summary>
    public CompiledProjection Stage(ProjectionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        CompiledProjection candidate = _projector.Compile(definition);
        lock (_gate)
        {
            if (!_rules.TryGetValue(candidate.Id, out RuleVersions? versions))
            {
                if (_rules.Count >= _maxRules) throw new ProjectionConfigurationException("Maximum number of registered rules exceeded.");
                _rules.Add(candidate.Id, versions = new RuleVersions());
            }
            if (versions.Plans.TryGetValue(candidate.Version, out CompiledProjection? old))
            {
                if (old.Fingerprint != candidate.Fingerprint)
                    throw new ProjectionConfigurationException("A published rule version cannot be replaced with different content.");
                return old;
            }
            if (versions.Plans.Count >= _maxVersionsPerRule)
                throw new ProjectionConfigurationException("Maximum versions per rule exceeded.");
            versions.Plans.Add(candidate.Version, candidate);
        }
        return candidate;
    }

    /// <summary>Compare-and-swap. null means no active version; a stale writer cannot override a newer active version.</summary>
    public bool TryActivate(string ruleId, int version, int? expectedActiveVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        lock (_gate)
        {
            if (!_rules.TryGetValue(ruleId, out RuleVersions? versions) ||
                versions.ActiveVersion != expectedActiveVersion || !versions.Plans.ContainsKey(version)) return false;
            versions.ActiveVersion = version;
            return true;
        }
    }

    public CompiledProjection? GetActive(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        lock (_gate)
        {
            return _rules.TryGetValue(ruleId, out RuleVersions? versions) && versions.ActiveVersion is int version
                ? versions.Plans[version] : null;
        }
    }

    public CompiledProjection? GetVersion(string ruleId, int version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        lock (_gate) return _rules.TryGetValue(ruleId, out RuleVersions? versions) &&
            versions.Plans.TryGetValue(version, out CompiledProjection? plan) ? plan : null;
    }

    public IReadOnlyList<int> ListVersions(string ruleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        lock (_gate) return _rules.TryGetValue(ruleId, out RuleVersions? versions)
            ? Array.AsReadOnly(versions.Plans.Keys.ToArray()) : Array.Empty<int>();
    }
}
