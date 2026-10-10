using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using Projecta.Json;
using Projecta.Objects;
using Projecta.SystemTextJson;

namespace Projecta.Mapping;

public sealed partial class ObjectProjector
{
    private JToken Evaluate(object? source, CompiledProjection projection, ProjectionExecutionContext context,
        CancellationToken cancellationToken, ProjectionExecutionState? state = null)
    {
        ArgumentNullException.ThrowIfNull(projection);
        long started = Stopwatch.GetTimestamp();
        using var activity = ProjectionTelemetry.ActivitySource.StartActivity("project");
        activity?.SetTag("projecta.plan", projection.Fingerprint);
        ProjectionTelemetry.Executions.Add(1);
        try
        {
            SetPhase(state, ProjectionPhase.Context);
            var environment = context.CreateEnvironment(_options.Functions ?? ProjectionFunctionCatalog.Empty, _options.Bindings, cancellationToken);
            SetPhase(state, ProjectionPhase.Source);
            JToken input = _options.EnableSelectiveReading && projection.InputContract is null && projection.Dependencies.CanSelectRootMembers
                ? _reader.ReadSelected(source, new HashSet<string>(projection.Dependencies.RootMembers, StringComparer.Ordinal), cancellationToken)
                : _reader.Read(source, cancellationToken);
            SetPhase(state, ProjectionPhase.InputContract);
            ValidateContract(projection.InputContract, input, cancellationToken);
            SetPhase(state, ProjectionPhase.Evaluate);
            JToken result = projection.Query.Eval(input, environment, _options.Evaluation, cancellationToken);
            if (result.Type == JTokenType.Undefined)
                throw new ProjectionMaterializationException("Projection returned undefined; provide a fallback in the rule.");
            TokenValidation.Validate(result, _options.Output, cancellationToken);
            SetPhase(state, ProjectionPhase.OutputContract);
            ValidateContract(projection.OutputContract, result, cancellationToken);
            return result;
        }
        catch
        {
            activity?.SetStatus(ActivityStatusCode.Error, "projection-failed");
            ProjectionTelemetry.Failures.Add(1);
            throw;
        }
        finally { ProjectionTelemetry.Duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }

    private static void SetPhase(ProjectionExecutionState? state, ProjectionPhase phase) { if (state is not null) state.Phase = phase; }
    private static void ValidateContract(IProjectionContract? contract, JToken value, CancellationToken token)
    {
        if (contract is null) return;
        var errors = contract.Validate(value, token);
        if (errors.Count != 0) throw new ProjectionContractException(errors);
    }

    public JToken ProjectToToken(object? source, ProjectionDefinition definition, CancellationToken cancellationToken = default)
        => ProjectToToken(source, Compile(definition, cancellationToken), cancellationToken);
    public JToken ProjectToToken(object? source, CompiledProjection projection, CancellationToken cancellationToken = default)
        => Evaluate(source, projection, ProjectionExecutionContext.Empty, cancellationToken).DeepClone();
    public JToken ProjectToTokenWithContext(object? source, CompiledProjection projection, ProjectionExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Evaluate(source, projection, context, cancellationToken).DeepClone();
    }

    public T? Project<T>(object? source, ProjectionDefinition definition, CancellationToken cancellationToken = default)
        => (T?)Project(source, typeof(T), Compile(definition, cancellationToken), cancellationToken);
    public T? Project<T>(object? source, CompiledProjection projection, CancellationToken cancellationToken = default)
        => (T?)Project(source, typeof(T), projection, cancellationToken);
    public object? Project(object? source, Type targetType, ProjectionDefinition definition, CancellationToken cancellationToken = default)
        => Project(source, targetType, Compile(definition, cancellationToken), cancellationToken);
    public object? Project(object? source, ProjectionDefinition definition, ProjectionTypeRegistry registry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(definition);
        if (string.IsNullOrWhiteSpace(definition.TargetType)) throw new ProjectionConfigurationException("A registered target alias is required.");
        return Project(source, registry.Resolve(definition.TargetType), Compile(definition, cancellationToken), cancellationToken);
    }
    public object? Project(object? source, Type targetType, CompiledProjection projection, CancellationToken cancellationToken = default)
    {
        ValidateTarget(targetType);
        return Materialize(Evaluate(source, projection, ProjectionExecutionContext.Empty, cancellationToken), targetType, cancellationToken);
    }

    public T? ProjectWithTypeInfo<T>(object? source, CompiledProjection projection, JsonTypeInfo<T> typeInfo,
        ProjectionExecutionContext? context = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        JToken result = Evaluate(source, projection, context ?? ProjectionExecutionContext.Empty, cancellationToken);
        T? value = JsonSerializer.Deserialize(NativeJsonWriter.ToUtf8Bytes(result, _options.Output, cancellationToken), typeInfo);
        cancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    public ProjectionResult<T> TryProject<T>(object? source, CompiledProjection projection,
        ProjectionExecutionContext? context = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var state = new ProjectionExecutionState { Phase = ProjectionPhase.Context };
        try
        {
            ValidateTarget(typeof(T));
            JToken result = Evaluate(source, projection, context ?? ProjectionExecutionContext.Empty, cancellationToken, state);
            state.Phase = ProjectionPhase.Materialize;
            return ProjectionResult<T>.Succeeded((T?)Materialize(result, typeof(T), cancellationToken));
        }
        catch (Exception ex) when (ProjectionErrors.CanReport(ex))
        { return ProjectionResult<T>.Failed(ProjectionErrors.Describe(ex, state.Phase, projection.Id, projection.Version, projection)); }
    }

    private static void ValidateTarget(Type targetType)
    {
        ArgumentNullException.ThrowIfNull(targetType);
        if (targetType.Assembly.IsDynamic || targetType.ContainsGenericParameters)
            throw new ProjectionConfigurationException("Dynamic or open generic target is not supported.");
    }

    private object? Materialize(JToken result, Type targetType, CancellationToken cancellationToken)
    {
        foreach (var materializer in _materializers)
            if (materializer.CanMaterialize(targetType))
            {
                var custom = materializer.Materialize(result.DeepClone(), targetType);
                cancellationToken.ThrowIfCancellationRequested();
                return custom;
            }
        if (typeof(JToken).IsAssignableFrom(targetType))
            return targetType.IsInstanceOfType(result) ? result.DeepClone() : throw new ProjectionMaterializationException("Unexpected native JSON shape.");
        if (typeof(JsonNode).IsAssignableFrom(targetType))
        {
            JsonNode? node = result.ToSystemTextJsonNode();
            return node is null || targetType.IsInstanceOfType(node) ? node : throw new ProjectionMaterializationException("Unexpected JSON node shape.");
        }
        byte[] bytes = NativeJsonWriter.ToUtf8Bytes(result, _options.Output, cancellationToken);
        if (targetType == typeof(JsonDocument)) return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = _options.Output.MaxDepth });
        if (targetType == typeof(JsonElement))
        { using var doc = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = _options.Output.MaxDepth }); return doc.RootElement.Clone(); }
        try
        {
            object? value = JsonSerializer.Deserialize(bytes, targetType, _json);
            cancellationToken.ThrowIfCancellationRequested();
            return value;
        }
        catch (Exception ex) when (ProjectionErrors.CanReport(ex))
        { throw new ProjectionMaterializationException("Projected data is incompatible with the requested target contract.", ex); }
    }
}
