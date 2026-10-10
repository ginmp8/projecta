using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.Objects;
using Projecta.SystemTextJson;

namespace Projecta.Documents;

internal sealed record PatchOperation(string Name, JsonPointer Path, JsonPointer? From, JToken? Value);

/// <summary>Atomic JSON document patches. Removing the root is deliberately rejected to preserve a JSON document.</summary>
public sealed class JsonPatchDocument
{
    private readonly PatchOperation[] _operations;
    private readonly DocumentOptions _options;
    private JsonPatchDocument(PatchOperation[] operations, DocumentOptions options) { _operations = operations; _options = options; }
    public int Count => _operations.Length;

    public static JsonPatchDocument Parse(string json, DocumentOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(json); options ??= new(); options.Validate();
        if (json.Length > options.MaxDefinitionCharacters) throw new ProjectionConfigurationException("Patch definition exceeds configured limit.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = options.Data.MaxDepth });
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > options.MaxOperations)
                throw new ProjectionConfigurationException("A bounded array of patch operations is required.");
            var operations = new List<PatchOperation>(); var reader = new ObjectReader(options.Data);
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object) throw new ProjectionConfigurationException("Invalid patch operation.");
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in element.EnumerateObject()) if (!names.Add(p.Name)) throw new ProjectionConfigurationException("Duplicate patch member.");
                string name = Text(element, "op"); JsonPointer path = JsonPointer.Parse(Text(element, "path"));
                if (name is not ("add" or "remove" or "replace" or "move" or "copy" or "test")) throw new ProjectionConfigurationException("Unknown patch operation.");
                JsonPointer? from = name is "move" or "copy" ? JsonPointer.Parse(Text(element, "from")) : null;
                JToken? value = null;
                if (name is "add" or "replace" or "test")
                {
                    if (!element.TryGetProperty("value", out var v)) throw new ProjectionConfigurationException("Patch value is required.");
                    value = reader.Read(v);
                }
                operations.Add(new(name, path, from, value));
            }
            return new(operations.ToArray(), options);
        }
        catch (JsonException ex) { throw new ProjectionConfigurationException("Patch must be valid JSON.", ex); }
    }
    internal static string Text(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) throw new ProjectionConfigurationException("Required operation member: " + name);
        return value.GetString()!;
    }
    public JToken Apply(JToken document, CancellationToken cancellationToken = default)
    {
        TokenValidation.Validate(document, _options.Data, cancellationToken);
        JToken result = document.DeepClone();
        for (int i = 0; i < _operations.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { result = ApplyOne(result, _operations[i]); TokenValidation.Validate(result, _options.Data, cancellationToken); }
            catch (DocumentTransformException ex) { throw new DocumentTransformException(ex.Code, i); }
        }
        return result;
    }
    internal static JToken ApplyOne(JToken document, PatchOperation operation)
    {
        JToken? value = operation.Value;
        string operationName = operation.Name;
        if (operationName == "test")
        {
            if (!operation.Path.Find(document, out var actual)) throw new DocumentTransformException("path-not-found");
            using var a = JsonDocument.Parse(NativeJsonWriter.ToUtf8Bytes(actual!));
            using var b = JsonDocument.Parse(NativeJsonWriter.ToUtf8Bytes(value!));
            if (!JsonElement.DeepEquals(a.RootElement, b.RootElement)) throw new DocumentTransformException("test-failed");
            return document;
        }
        if (operationName is "move" or "copy")
        {
            if (!operation.From!.Find(document, out value)) throw new DocumentTransformException("source-not-found");
            value = value!.DeepClone();
            if (operationName == "move")
            {
                if (operation.From.Value == operation.Path.Value) return document;
                if (operation.From.IsStrictAncestorOf(operation.Path)) throw new DocumentTransformException("move-into-child");
                document = Rewrite(document, operation.From.Segments, 0, "remove", null);
            }
            operationName = "add";
        }
        return Rewrite(document, operation.Path.Segments, 0, operationName, value?.DeepClone());
    }
    private static JToken Rewrite(JToken current, string[] segments, int depth, string op, JToken? value)
    {
        if (depth == segments.Length)
        {
            if (op == "remove") throw new DocumentTransformException("root-remove-not-supported");
            return value!;
        }
        string segment = segments[depth]; bool last = depth == segments.Length - 1;
        if (current is JObject o)
        {
            bool exists = o.Properties.TryGetValue(segment, out var child);
            if (last)
            {
                if (op != "add" && !exists) throw new DocumentTransformException("path-not-found");
                if (op == "remove") o.Remove(segment); else o.Set(segment, value!);
            }
            else
            {
                if (!exists) throw new DocumentTransformException("parent-not-found");
                o.Set(segment, Rewrite(child!, segments, depth + 1, op, value));
            }
            return o;
        }
        if (current is JArray a)
        {
            if (!JsonPointer.Index(segment, a.Count, last && op == "add", out int index)) throw new DocumentTransformException("invalid-array-index");
            var result = new JArray();
            for (int i = 0; i <= a.Count; i++)
            {
                if (i == index && last && op == "add") result.Add(value!);
                if (i == a.Count) break;
                if (i != index || last && op == "add") result.Add(a.ChildrenTokens[i]);
                else if (!last) result.Add(Rewrite(a.ChildrenTokens[i], segments, depth + 1, op, value));
                else if (op == "replace") result.Add(value!);
            }
            return result;
        }
        throw new DocumentTransformException("parent-is-scalar");
    }
}
