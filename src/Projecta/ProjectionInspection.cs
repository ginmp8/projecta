using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Projecta.Json;

namespace Projecta.Mapping;

public enum ProjectionIssueSeverity { Information, Warning, Error }

/// <summary>Static evidence only. A dynamic expression is never labeled valid for a destination without a runtime check.</summary>
public sealed record ProjectionPreflightIssue(string Code, ProjectionIssueSeverity Severity,
    string Message, string? Member = null);

public sealed record ProjectionPlanExplanation(string RuleId, int RuleVersion, string? Fingerprint,
    bool CanSelectRootMembers, IReadOnlyList<string> Paths, string ReadMode, string? FallbackReason,
    bool HasInputContract, bool HasOutputContract);

public sealed record ProjectionPreflightReport(ProjectionPlanExplanation Explanation,
    IReadOnlyList<ProjectionPreflightIssue> Issues)
{
    public bool Success => !Issues.Any(x => x.Severity == ProjectionIssueSeverity.Error);
}

/// <summary>Reads metadata and query AST only. It never evaluates a rule or invokes a source object's getters.</summary>
public sealed class ProjectionInspector
{
    private readonly ObjectProjector _projector;
    public ProjectionInspector(ObjectProjector projector) => _projector = projector ?? throw new ArgumentNullException(nameof(projector));

    public ProjectionPlanExplanation Explain(CompiledProjection plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        bool possible = plan.Dependencies.CanSelectRootMembers;
        bool enabled = _projector.Options.EnableSelectiveReading;
        bool contract = plan.InputContract is not null;
        var paths = plan.Dependencies.PropertyPaths
            .Select(path => "/" + string.Join('/', path.Select(Escape)))
            .Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return new(plan.Id, plan.Version, plan.Fingerprint, possible,
            Array.AsReadOnly(paths), enabled && possible && !contract ? "selective-poco" : "full",
            contract ? "input-contract-requires-complete-source" : !possible ? plan.Dependencies.FallbackReason :
                !enabled ? "selective-reading-disabled" : null,
            contract, plan.OutputContract is not null);
    }

    public ProjectionPreflightReport Inspect(ProjectionDefinition definition, Type? sourceType = null, Type? targetType = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var compiled = _projector.TryCompile(definition);
        if (!compiled.Success)
        {
            return new(new(definition.Id, definition.Version, null, false, Array.Empty<string>(), "unknown",
                "compilation-failed", definition.InputSchema.HasValue, definition.OutputSchema.HasValue),
                Array.AsReadOnly(compiled.Diagnostics.Select(d =>
                    new ProjectionPreflightIssue("PX_COMPILE", ProjectionIssueSeverity.Error, d.Message, d.Field)).ToArray()));
        }
        CompiledProjection plan = compiled.Value!;
        var issues = new List<ProjectionPreflightIssue>();
        if (sourceType is not null && IsPlainObject(sourceType) && plan.Dependencies.CanSelectRootMembers)
        {
            var exposed = _projector.Options.Source.AllowedRootMembers is null ? null :
                new HashSet<string>(_projector.Options.Source.AllowedRootMembers, StringComparer.Ordinal);
            var names = sourceType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(p => p.GetMethod is { IsPublic: true, IsStatic: false } && p.GetIndexParameters().Length == 0)
                .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always)
                .Where(p => _projector.Options.Source.IncludeProperty?.Invoke(sourceType, p) != false)
                .Select(p => _projector.Options.Source.UseJsonPropertyNames ? p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name : p.Name)
                .ToHashSet(StringComparer.Ordinal);
            foreach (string member in plan.Dependencies.RootMembers)
            {
                if (!names.Contains(member)) issues.Add(new("PX_SOURCE_MEMBER", ProjectionIssueSeverity.Warning,
                    "Source type does not declare the member; the expression may handle its absence at runtime.", member));
                else if (exposed is not null && !exposed.Contains(member))
                    issues.Add(new("PX_SOURCE_EXPOSURE", ProjectionIssueSeverity.Error,
                        "Source exposure policy excludes the required member.", member));
            }
        }
        if (targetType is not null && IsPlainObject(targetType))
        {
            if (definition.Fields is { Count: > 0 } fields && string.IsNullOrWhiteSpace(definition.Expression))
            {
                var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.SetMethod is { IsPublic: true } || p.GetCustomAttribute<RequiredMemberAttribute>() is not null)
                    .Where(p => p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always)
                    .ToArray();
                var targetNames = properties.Select(p => p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? p.Name).ToHashSet(StringComparer.Ordinal);
                foreach (string field in fields.Keys)
                    if (!targetNames.Contains(field)) issues.Add(new("PX_TARGET_FIELD", ProjectionIssueSeverity.Error,
                        "Rule writes a member not available on the target contract.", field));
                foreach (PropertyInfo prop in properties)
                {
                    if (prop.GetCustomAttribute<JsonRequiredAttribute>() is null &&
                        prop.GetCustomAttribute<RequiredMemberAttribute>() is null) continue;
                    string name = prop.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? prop.Name;
                    if (!fields.ContainsKey(name)) issues.Add(new("PX_TARGET_REQUIRED", ProjectionIssueSeverity.Error,
                        "Rule omits a required target member.", name));
                }
            }
            else issues.Add(new("PX_TARGET_INDETERMINATE", ProjectionIssueSeverity.Warning,
                "Destination shape depends on evaluating the expression; runtime validation is required."));
        }
        return new(Explain(plan), Array.AsReadOnly(issues.ToArray()));
    }

    private static bool IsPlainObject(Type type) => !type.IsPrimitive && !type.IsEnum &&
        type != typeof(string) && !typeof(IEnumerable).IsAssignableFrom(type) &&
        !typeof(JToken).IsAssignableFrom(type) && !typeof(JsonNode).IsAssignableFrom(type) &&
        type != typeof(JsonDocument) && type != typeof(JsonElement) && !type.Assembly.IsDynamic;

    internal static string Escape(string key) => key.Replace("~", "~0", StringComparison.Ordinal)
        .Replace("/", "~1", StringComparison.Ordinal);
}
