using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Projecta.Objects;

namespace Projecta.Mapping;

/// <summary>Evidence of plan/configuration identity, not a guarantee of deterministic host functions or source getters.</summary>
public sealed record ProjectionReplayIdentity(string RuleId, int RuleVersion, string PlanFingerprint,
    string ConfigurationFingerprint, bool CanReproduce, IReadOnlyList<string> Limitations)
{
    /// <summary>No binding values are included in the fingerprint; an external binding identity is required for replay.</summary>
    public static ProjectionReplayIdentity Create(CompiledProjection plan, ProjectionOptions options,
        ProjectionExecutionContext? context = null, string? bindingIdentity = null,
        string? sourceIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(options);
        context ??= ProjectionExecutionContext.Empty;
        if (bindingIdentity is not null && (bindingIdentity.Length is < 1 or > 256 ||
            bindingIdentity.Any(x => char.IsControl(x)))) throw new ArgumentException("Invalid binding provenance identifier.", nameof(bindingIdentity));
        if (sourceIdentity is not null && (sourceIdentity.Length is < 1 or > 256 ||
            sourceIdentity.Any(x => char.IsControl(x)))) throw new ArgumentException("Invalid source provenance identifier.", nameof(sourceIdentity));
        var limits = new[] { ("bindings", options.Bindings), ("output", options.Output), ("source", options.Source) }
            .Select(p => new
            {
                p.Item1, p.Item2.MaxDepth, p.Item2.MaxNodes, p.Item2.MaxCharacters,
                p.Item2.UseJsonPropertyNames,
                Allow = p.Item2.AllowedRootMembers?.OrderBy(s => s, StringComparer.Ordinal).ToArray(),
                HasPropertyPredicate = p.Item2.IncludeProperty is not null
            }).ToArray();
        var profile = new
        {
            Version = 1,
            plan.Fingerprint,
            options.EnableSelectiveReading,
            NullProperties = options.NullProperties.ToString(),
            options.CacheCapacity,
            options.MaxConcurrentCompilations,
            Limits = limits,
            options.Evaluation.MaxSteps,
            options.Evaluation.MaxAllocatedTokens,
            options.Evaluation.MaxRangeLength,
            options.Evaluation.MaxStringLength,
            options.Evaluation.MaxDepth,
            TimeoutTicks = options.Evaluation.Timeout.Ticks,
            options.Evaluation.AllowDynamicEvaluation,
            Variables = context.VariableNames,
            Functions = context.AllowedFunctionNames,
            context.AllowImpureFunctions,
            HasHostFunctions = options.Functions is not null && options.Functions != ProjectionFunctionCatalog.Empty,
            BindingIdentity = bindingIdentity,
            SourceIdentity = sourceIdentity
        };
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(profile);
        string hash = Convert.ToHexStringLower(SHA256.HashData(json));
        var warnings = new List<string>();
        if (sourceIdentity is null) warnings.Add("source-values-unpinned");
        if (context.VariableNames.Count > 0 && bindingIdentity is null)
            warnings.Add("binding-values-unpinned");
        if (context.AllowedFunctionNames.Count > 0)
            warnings.Add("host-function-purity-unverified");
        if (options.Source.IncludeProperty is not null || options.Output.IncludeProperty is not null || options.Bindings.IncludeProperty is not null)
            warnings.Add("property-predicate-unversioned");
        if (options.Functions is not null && options.Functions != ProjectionFunctionCatalog.Empty)
            warnings.Add("host-catalog-unversioned");
        return new(plan.Id, plan.Version, plan.Fingerprint, hash, warnings.Count == 0,
            Array.AsReadOnly(warnings.ToArray()));
    }
}
