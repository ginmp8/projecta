using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Projecta.Execution;
using Projecta.Mapping;

namespace Projecta.Streaming;

public enum BatchErrorMode { Continue, Stop }
public sealed record ProjectionBatchOptions
{
    public BatchErrorMode ErrorMode { get; init; }
    public long MaxRecords { get; init; } = 1_000_000;
    public int MaxLineBytes { get; init; } = 1_000_000;
    public bool SkipBlankLines { get; init; } = true;
    public int MaxJsonDepth { get; init; } = 64;
    internal void Validate()
    {
        if (!Enum.IsDefined(ErrorMode) || MaxRecords < 1 || MaxLineBytes is < 1 or > 64_000_000 || MaxJsonDepth is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(ProjectionBatchOptions));
    }
}
public sealed record ProjectionItemResult<T>(long Index, ProjectionResult<T> Result, long? LineNumber = null);
public sealed class ProjectionBatchException : Exception
{
    public long Index { get; }
    public IReadOnlyList<ProjectionDiagnostic> Diagnostics { get; }
    internal ProjectionBatchException(long index, IReadOnlyList<ProjectionDiagnostic> diagnostics) : base("Record projection failed.")
    { Index = index; Diagnostics = diagnostics; }
}

/// <summary>Sequential, pull-based execution. The caller owns source lifetime; a DbContext is never used concurrently.</summary>
public static class ProjectionBatch
{
    public static IEnumerable<ProjectionItemResult<T>> ProjectMany<T>(this ObjectProjector projector, IEnumerable<object?> source,
        CompiledProjection plan, ProjectionExecutionContext? context = null, ProjectionBatchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projector); ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(plan);
        options ??= new(); options.Validate(); long index = 0;
        cancellationToken.ThrowIfCancellationRequested();
        foreach (object? item in source)
        {
            CheckRecord(index, options, cancellationToken);
            var result = projector.TryProject<T>(item, plan, context, cancellationToken);
            Enforce(index, result, options);
            yield return new(index++, result);
        }
    }

    public static async IAsyncEnumerable<ProjectionItemResult<T>> ProjectManyAsync<T>(this ObjectProjector projector,
        IAsyncEnumerable<object?> source, CompiledProjection plan, ProjectionExecutionContext? context = null,
        ProjectionBatchOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projector); ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(plan);
        options ??= new(); options.Validate(); long index = 0;
        cancellationToken.ThrowIfCancellationRequested();
        await foreach (object? item in source.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            CheckRecord(index, options, cancellationToken);
            var result = projector.TryProject<T>(item, plan, context, cancellationToken);
            Enforce(index, result, options);
            yield return new(index++, result);
        }
    }

    public static async IAsyncEnumerable<ProjectionItemResult<T>> ProjectJsonLinesAsync<T>(this ObjectProjector projector,
        Stream source, CompiledProjection plan, ProjectionExecutionContext? context = null, ProjectionBatchOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projector); ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(plan);
        if (!source.CanRead) throw new ArgumentException("Input stream must be readable.", nameof(source));
        options ??= new(); options.Validate(); long index = 0;
        await foreach (var line in BoundedLines.Read(source, options.MaxLineBytes, cancellationToken).ConfigureAwait(false))
        {
            if (options.SkipBlankLines && IsBlank(line.Bytes)) continue;
            CheckRecord(index, options, cancellationToken);
            ProjectionResult<T> result;
            try
            {
                using var input = JsonDocument.Parse(line.Bytes, new JsonDocumentOptions { MaxDepth = options.MaxJsonDepth });
                result = projector.TryProject<T>(input.RootElement, plan, context, cancellationToken);
            }
            catch (JsonException)
            {
                result = ProjectionResult<T>.Failed(new[] { new ProjectionDiagnostic("invalid-json", ProjectionPhase.Stream,
                    plan.Id, plan.Version, "Record is not valid JSON within the configured depth limit.") });
            }
            Enforce(index, result, options);
            yield return new(index++, result, line.Number);
        }
    }
    private static bool IsBlank(ReadOnlyMemory<byte> memory)
    {
        foreach (byte b in memory.Span) if (b is not (9 or 13 or 32)) return false;
        return true;
    }
    private static void CheckRecord(long index, ProjectionBatchOptions options, CancellationToken token)
    { token.ThrowIfCancellationRequested(); if (index >= options.MaxRecords) throw new ExecutionLimitException("batch-records"); }
    private static void Enforce<T>(long index, ProjectionResult<T> result, ProjectionBatchOptions options)
    { if (!result.Success && options.ErrorMode == BatchErrorMode.Stop) throw new ProjectionBatchException(index, result.Diagnostics); }
}
