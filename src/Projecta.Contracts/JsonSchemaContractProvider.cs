using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Projecta.Mapping;

namespace Projecta.Contracts;

/// <summary>Compiles the bounded projecta-contract-v1 structural profile. Unsupported keywords fail closed.</summary>
public sealed class JsonSchemaContractProvider : IProjectionContractProvider
{
    private readonly JsonSchemaContractOptions _options;
    public JsonSchemaContractProvider(JsonSchemaContractOptions? options = null)
    { _options = options ?? new(); _options.Validate(); }

    public IProjectionContract Compile(JsonElement schema)
    {
        try
        {
            if (schema.GetRawText().Length > _options.MaxSchemaCharacters) throw Invalid();
            JsonElement frozen = schema.Clone();
            var regex = new Dictionary<string, Regex>(StringComparer.Ordinal);
            var references = new List<string>();
            int nodes = 0;
            void Pattern(string pattern)
            {
                if (pattern.Length > _options.MaxPatternLength) throw Invalid();
                if (!regex.ContainsKey(pattern)) regex.Add(pattern, new Regex(pattern, RegexOptions.CultureInvariant, _options.RegexTimeout));
            }
            void Visit(JsonElement s, int depth)
            {
                if (++nodes > _options.MaxSchemaNodes || depth > _options.MaxDepth) throw Invalid();
                if (s.ValueKind is JsonValueKind.True or JsonValueKind.False) return;
                if (s.ValueKind != JsonValueKind.Object) throw Invalid();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in s.EnumerateObject())
                {
                    if (!seen.Add(p.Name)) throw Invalid();
                    JsonElement v = p.Value;
                    switch (p.Name)
                    {
                        case "$schema":
                            Require(v, JsonValueKind.String);
                            if (v.GetString() is not ("https://json-schema.org/draft/2020-12/schema" or "projecta-contract-v1")) throw Invalid();
                            break;
                        case "$id":
                            Require(v, JsonValueKind.String);
                            if (depth != 1) throw Invalid(); // Nested URI scopes are outside this local-reference profile.
                            break;
                        case "title": case "description": case "$comment": Require(v, JsonValueKind.String); break;
                        case "default": break; // Annotation only; never applied to data.
                        case "examples": Require(v, JsonValueKind.Array); break;
                        case "$ref":
                            Require(v, JsonValueKind.String);
                            string reference = v.GetString()!;
                            if (reference != "#" && !reference.StartsWith("#/", StringComparison.Ordinal)) throw Invalid();
                            _ = Resolve(frozen, reference); references.Add(reference); break;
                        case "$defs": case "properties": case "patternProperties": case "dependentSchemas":
                            Require(v, JsonValueKind.Object);
                            var keys = new HashSet<string>(StringComparer.Ordinal);
                            foreach (var child in v.EnumerateObject())
                            {
                                if (!keys.Add(child.Name)) throw Invalid();
                                if (p.Name == "patternProperties") Pattern(child.Name);
                                Visit(child.Value, depth + 1);
                            }
                            break;
                        case "additionalProperties": case "propertyNames": case "items": case "contains":
                        case "not": case "if": case "then": case "else": Visit(v, depth + 1); break;
                        case "prefixItems": case "allOf": case "anyOf": case "oneOf":
                            Require(v, JsonValueKind.Array); if (v.GetArrayLength() == 0) throw Invalid();
                            foreach (var child in v.EnumerateArray()) Visit(child, depth + 1); break;
                        case "type":
                            foreach (var name in StringSet(v, allowSingle: true))
                                if (name is not ("null" or "boolean" or "object" or "array" or "number" or "integer" or "string")) throw Invalid();
                            break;
                        case "required": _ = StringSet(v); break;
                        case "dependentRequired":
                            Require(v, JsonValueKind.Object);
                            foreach (var child in v.EnumerateObject()) _ = StringSet(child.Value); break;
                        case "minProperties": case "maxProperties": case "minItems": case "maxItems":
                        case "minContains": case "maxContains": case "minLength": case "maxLength":
                            Require(v, JsonValueKind.Number); if (!v.TryGetInt32(out int n) || n < 0) throw Invalid(); break;
                        case "minimum": case "maximum": case "exclusiveMinimum": case "exclusiveMaximum": case "multipleOf":
                            Require(v, JsonValueKind.Number); var number = JsonNumber.Parse(v.GetRawText());
                            if (p.Name == "multipleOf" && number.Coefficient <= 0) throw Invalid(); break;
                        case "uniqueItems":
                            if (v.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Invalid(); break;
                        case "pattern": Require(v, JsonValueKind.String); Pattern(v.GetString()!); break;
                        case "enum": Require(v, JsonValueKind.Array); if (v.GetArrayLength() == 0) throw Invalid(); break;
                        case "const": break;
                        default: throw new ProjectionConfigurationException("Unsupported contract keyword: " + p.Name);
                    }
                }
            }
            Visit(frozen, 1);
            // References must target actual schemas, including references into $defs.
            var checkedReferences = new HashSet<string>(StringComparer.Ordinal) { "#" };
            for (int i = 0; i < references.Count; i++)
                if (checkedReferences.Add(references[i])) Visit(Resolve(frozen, references[i]), 1);
            return new JsonSchemaContract(frozen, _options, regex);
        }
        catch (ProjectionConfigurationException) { throw; }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException or FormatException)
        { throw new ProjectionConfigurationException("Invalid structural contract.", ex); }
    }

    internal static JsonElement Resolve(JsonElement root, string reference)
    {
        if (reference == "#") return root;
        string fragment;
        try { fragment = Uri.UnescapeDataString(reference[1..]); }
        catch (UriFormatException) { throw Invalid(); }
        foreach (string segment in fragment[1..].Split('/'))
        {
            var b = new System.Text.StringBuilder();
            for (int i = 0; i < segment.Length; i++)
            {
                if (segment[i] != '~') b.Append(segment[i]);
                else if (++i < segment.Length && segment[i] is '0' or '1') b.Append(segment[i] == '0' ? '~' : '/');
                else throw Invalid();
            }
            string key = b.ToString();
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(key, out var child)) root = child;
            else if (root.ValueKind == JsonValueKind.Array && int.TryParse(key, out int index) && index >= 0 && index < root.GetArrayLength()) root = root[index];
            else throw Invalid();
        }
        return root;
    }
    private static void Require(JsonElement v, JsonValueKind kind) { if (v.ValueKind != kind) throw Invalid(); }
    private static string[] StringSet(JsonElement value, bool allowSingle = false)
    {
        if (allowSingle && value.ValueKind == JsonValueKind.String) return new[] { value.GetString()! };
        Require(value, JsonValueKind.Array);
        var values = new List<string>(); var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in value.EnumerateArray())
        { Require(child, JsonValueKind.String); string s = child.GetString()!; if (!unique.Add(s)) throw Invalid(); values.Add(s); }
        if (allowSingle && values.Count == 0) throw Invalid();
        return values.ToArray();
    }
    private static ProjectionConfigurationException Invalid() => new("Invalid or unsupported structural contract.");
}
