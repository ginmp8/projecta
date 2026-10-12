using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Projecta.Json;

namespace Projecta.Mapping;

public enum ProjectionDifferenceKind { Added, Removed, ValueChanged, TypeChanged }

/// <summary>Shape-only difference: output values are deliberately excluded from reports.</summary>
public sealed record ProjectionDifference(string JsonPointer, ProjectionDifferenceKind Kind);

public sealed record ProjectionComparisonReport(
    string BeforeRuleId, int BeforeVersion, string BeforeFingerprint, bool BeforeSucceeded,
    string AfterRuleId, int AfterVersion, string AfterFingerprint, bool AfterSucceeded,
    bool Equivalent, bool Truncated, IReadOnlyList<ProjectionDifference> Differences,
    IReadOnlyList<ProjectionDiagnostic> BeforeDiagnostics, IReadOnlyList<ProjectionDiagnostic> AfterDiagnostics);

/// <summary>Runs the two immutable plans against identical source and call context; sources with side-effecting getters are not replay-safe.</summary>
public sealed class ProjectionComparer
{
    private readonly ObjectProjector _projector;
    public ProjectionComparer(ObjectProjector projector) => _projector = projector ?? throw new ArgumentNullException(nameof(projector));

    public ProjectionComparisonReport Compare(object? source, CompiledProjection before, CompiledProjection after,
        ProjectionExecutionContext? context = null, int maxDifferences = 128, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        if (maxDifferences is < 1 or > 1024) throw new ArgumentOutOfRangeException(nameof(maxDifferences));
        cancellationToken.ThrowIfCancellationRequested();
        ProjectionExecutionContext shared = context ?? ProjectionExecutionContext.Empty;
        ProjectionResult<JToken> oldResult = _projector.TryProject<JToken>(source, before, shared, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        ProjectionResult<JToken> newResult = _projector.TryProject<JToken>(source, after, shared, cancellationToken);
        var changes = new List<ProjectionDifference>();
        bool truncated = false;
        if (oldResult.Success && newResult.Success && oldResult.Value is not null && newResult.Value is not null)
            CompareNodes(oldResult.Value, newResult.Value, "", 0);
        return new(before.Id, before.Version, before.Fingerprint, oldResult.Success,
            after.Id, after.Version, after.Fingerprint, newResult.Success,
            oldResult.Success && newResult.Success && changes.Count == 0,
            truncated, changes.AsReadOnly(), oldResult.Diagnostics, newResult.Diagnostics);

        void Add(string pointer, ProjectionDifferenceKind kind)
        {
            if (changes.Count < maxDifferences) changes.Add(new(pointer, kind));
            else truncated = true;
        }

        void CompareNodes(JToken prior, JToken next, string pointer, int depth)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (truncated || depth > 256) { truncated = true; return; }
            if (prior.Type != next.Type) { Add(pointer, ProjectionDifferenceKind.TypeChanged); return; }
            if (prior is JObject left && next is JObject right)
            {
                foreach (string name in left.Keys.Concat(right.Keys).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal))
                {
                    if (truncated) return;
                    bool had = left.Properties.TryGetValue(name, out JToken? oldNode);
                    bool has = right.Properties.TryGetValue(name, out JToken? newNode);
                    string path = pointer + "/" + ProjectionInspector.Escape(name);
                    if (!had) Add(path, ProjectionDifferenceKind.Added);
                    else if (!has) Add(path, ProjectionDifferenceKind.Removed);
                    else CompareNodes(oldNode!, newNode!, path, depth + 1);
                }
                return;
            }
            if (prior is JArray first && next is JArray second)
            {
                int n = Math.Max(first.Count, second.Count);
                for (int i = 0; i < n; i++)
                {
                    if (truncated) return;
                    string path = pointer + "/" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    if (i >= first.Count) Add(path, ProjectionDifferenceKind.Added);
                    else if (i >= second.Count) Add(path, ProjectionDifferenceKind.Removed);
                    else CompareNodes(first.ChildrenTokens[i], second.ChildrenTokens[i], path, depth + 1);
                }
                return;
            }
            if (!JToken.DeepEquals(prior, next)) Add(pointer, ProjectionDifferenceKind.ValueChanged);
        }
    }
}
