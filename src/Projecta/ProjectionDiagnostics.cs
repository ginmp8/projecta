using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using Projecta.Execution;
using Projecta.Objects;

namespace Projecta.Mapping;

public enum ProjectionPhase { Compile, Context, Source, InputContract, Evaluate, OutputContract, Materialize, Stream }

/// <summary>Safe diagnostic metadata. It intentionally excludes input, expression text, raw exception messages and stack traces.</summary>
public sealed record ProjectionDiagnostic(
    string Code, ProjectionPhase Phase, string RuleId, int RuleVersion, string Message,
    string? Field = null, int? Position = null, string? JsonPointer = null,
    string? EngineCode = null, string? ExpectedType = null, string? ActualType = null);

public sealed class ProjectionResult<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public IReadOnlyList<ProjectionDiagnostic> Diagnostics { get; }
    private ProjectionResult(bool success, T? value, IEnumerable<ProjectionDiagnostic> diagnostics)
    { Success = success; Value = value; Diagnostics = new ReadOnlyCollection<ProjectionDiagnostic>(diagnostics.ToArray()); }
    public static ProjectionResult<T> Succeeded(T? value) => new(true, value, Array.Empty<ProjectionDiagnostic>());
    public static ProjectionResult<T> Failed(IEnumerable<ProjectionDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var result = new ProjectionResult<T>(false, default, diagnostics);
        if (result.Diagnostics.Count == 0) throw new ArgumentException("A failed result needs a diagnostic.", nameof(diagnostics));
        return result;
    }
}

internal sealed class ProjectionExecutionState { internal ProjectionPhase Phase { get; set; } }

internal static class ProjectionErrors
{
    internal static IReadOnlyList<ProjectionDiagnostic> Describe(Exception exception, ProjectionPhase phase,
        string ruleId, int version, CompiledProjection? plan = null)
    {
        if (exception is ProjectionContractException contracts)
            return contracts.Violations.Select(v => new ProjectionDiagnostic("PX_CONTRACT", phase, ruleId, version,
                "Data does not satisfy the declared contract.", JsonPointer: v.JsonPointer,
                ExpectedType: v.ExpectedType, ActualType: v.ActualType)).ToArray();
        JsonataException? engine = Find<JsonataException>(exception);
        string code = exception switch
        {
            ExecutionLimitException => "PX_LIMIT",
            ObjectReadException => "PX_SOURCE",
            ProjectionConfigurationException => "PX_CONFIGURATION",
            ProjectionMaterializationException or JsonException => "PX_TARGET",
            _ when engine is not null => "PX_EXPRESSION",
            _ => "PX_EXECUTION"
        };
        int? position = engine?.Position is >= 0 ? engine.Position : null;
        return new[] { new ProjectionDiagnostic(code, phase, ruleId, version,
            "Projection failed during " + phase.ToString().ToLowerInvariant() + ".",
            plan?.FindField(position), position, EngineCode: engine?.Code.ToString()) };
    }

    private static T? Find<T>(Exception? ex) where T : Exception
    {
        for (int depth = 0; ex is not null && depth < 16; ex = ex.InnerException, depth++)
            if (ex is T result) return result;
        return null;
    }

    internal static bool CanReport(Exception ex) => ex is not OperationCanceledException and not OutOfMemoryException;
}
