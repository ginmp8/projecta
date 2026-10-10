using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.Objects;

namespace Projecta.Documents;

public sealed record DocumentStageTrace(int Index, string Operation, double ElapsedMilliseconds);
public sealed record DocumentPipelineResult(JToken Value, IReadOnlyList<DocumentStageTrace> Trace);

/// <summary>Versioned, immutable operation plan. Data changes are isolated; trusted host callbacks can still have external side effects.</summary>
public sealed class DocumentPipeline
{
    private sealed record Stage(string Name, JsonPointer? Path, JsonPointer? From, JToken? Value, CompiledProjection? Plan);
    private readonly Stage[] _stages;
    private readonly DocumentOptions _options;
    private readonly ObjectProjector _projector;
    private DocumentPipeline(Stage[] stages, DocumentOptions options, ObjectProjector projector)
    { _stages = stages; _options = options; _projector = projector; }

    public static DocumentPipeline Parse(string json, ObjectProjector? projector = null, DocumentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(json); projector ??= new(); options ??= new(); options.Validate();
        if (json.Length > options.MaxDefinitionCharacters) throw new ProjectionConfigurationException("Pipeline definition exceeds configured limit.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = options.Data.MaxDepth });
            var root = document.RootElement;
            CheckMembers(root, "version", "steps");
            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int v) || v != 1 ||
                !root.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array || steps.GetArrayLength() > options.MaxOperations)
                throw new ProjectionConfigurationException("Pipeline version 1 and bounded steps are required.");
            var stages = new List<Stage>(); var reader = new ObjectReader(options.Data);
            foreach (var s in steps.EnumerateArray())
            {
                string name = JsonPatchDocument.Text(s, "op");
                if (name == "project")
                {
                    CheckMembers(s, "op", "expression");
                    stages.Add(new(name, null, null, null, projector.Compile(new ProjectionDefinition { Expression = JsonPatchDocument.Text(s, "expression") })));
                    continue;
                }
                if (name is not ("copy" or "rename" or "default" or "omit" or "ensureArray")) throw new ProjectionConfigurationException("Unknown pipeline operation.");
                CheckMembers(s, name is "copy" or "rename" ? new[] { "op", "path", "from" } : name == "default" ? new[] { "op", "path", "value" } : new[] { "op", "path" });
                JsonPointer path = JsonPointer.Parse(JsonPatchDocument.Text(s, "path"));
                JsonPointer? from = name is "copy" or "rename" ? JsonPointer.Parse(JsonPatchDocument.Text(s, "from")) : null;
                JToken? value = null;
                if (name == "default")
                {
                    if (!s.TryGetProperty("value", out var data)) throw new ProjectionConfigurationException("Default value is required.");
                    value = reader.Read(data);
                }
                stages.Add(new(name, path, from, value, null));
            }
            return new(stages.ToArray(), options, projector);
        }
        catch (JsonException ex) { throw new ProjectionConfigurationException("Pipeline must be valid JSON.", ex); }
    }
    public DocumentPipelineResult Execute(JToken document, ProjectionExecutionContext? context = null, CancellationToken cancellationToken = default)
    {
        TokenValidation.Validate(document, _options.Data, cancellationToken);
        JToken current = document.DeepClone(); var trace = new List<DocumentStageTrace>();
        for (int i = 0; i < _stages.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested(); Stage stage = _stages[i]; long start = Stopwatch.GetTimestamp();
            try
            {
                if (stage.Name == "project") current = _projector.ProjectToTokenWithContext(current, stage.Plan!, context ?? ProjectionExecutionContext.Empty, cancellationToken);
                else
                {
                    bool exists = stage.Path!.Find(current, out var value);
                    PatchOperation? operation = stage.Name switch
                    {
                        "copy" => new("copy", stage.Path, stage.From, null),
                        "rename" => new("move", stage.Path, stage.From, null),
                        "default" when !exists => new("add", stage.Path, null, stage.Value),
                        "omit" when exists => new("remove", stage.Path, null, null),
                        "ensureArray" when !exists => new("add", stage.Path, null, new JArray()),
                        "ensureArray" when value is not JArray => new("replace", stage.Path, null, Singleton(value!)),
                        _ => null
                    };
                    if (operation is not null) current = JsonPatchDocument.ApplyOne(current, operation);
                }
                TokenValidation.Validate(current, _options.Data, cancellationToken);
            }
            catch (DocumentTransformException ex) { throw new DocumentTransformException(ex.Code, i); }
            trace.Add(new(i, stage.Name, Stopwatch.GetElapsedTime(start).TotalMilliseconds));
        }
        return new(current, new ReadOnlyCollection<DocumentStageTrace>(trace));
    }
    private static JArray Singleton(JToken value) { var result = new JArray(); result.Add(value.DeepClone()); return result; }
    private static void CheckMembers(JsonElement element, params string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object) throw new ProjectionConfigurationException("An operation object is required.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in element.EnumerateObject()) if (!seen.Add(p.Name) || !allowed.Contains(p.Name, StringComparer.Ordinal))
            throw new ProjectionConfigurationException("Unknown or duplicate pipeline member.");
    }
}
