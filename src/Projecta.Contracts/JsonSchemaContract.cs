using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using Projecta.Execution;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.SystemTextJson;

namespace Projecta.Contracts;

internal sealed class JsonSchemaContract : IProjectionContract
{
    private readonly JsonElement _schema;
    private readonly JsonSchemaContractOptions _options;
    private readonly IReadOnlyDictionary<string, Regex> _patterns;
    internal JsonSchemaContract(JsonElement schema, JsonSchemaContractOptions options, IReadOnlyDictionary<string, Regex> patterns)
    { _schema = schema; _options = options; _patterns = patterns; }

    public IReadOnlyList<ContractViolation> Validate(JToken value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var input = JsonDocument.Parse(NativeJsonWriter.ToUtf8Bytes(value, _options.DataLimits, cancellationToken),
            new JsonDocumentOptions { MaxDepth = _options.DataLimits.MaxDepth });
        var state = new State(_schema, _options, _patterns, cancellationToken);
        var errors = new List<ContractViolation>();
        state.Evaluate(_schema, input.RootElement, "", 1, errors);
        return errors.AsReadOnly();
    }

    private sealed class State
    {
        private readonly JsonElement _root;
        private readonly JsonSchemaContractOptions _options;
        private readonly IReadOnlyDictionary<string, Regex> _patterns;
        private readonly CancellationToken _cancellation;
        private int _steps;
        internal State(JsonElement root, JsonSchemaContractOptions options, IReadOnlyDictionary<string, Regex> patterns, CancellationToken cancellation)
        { _root = root; _options = options; _patterns = patterns; _cancellation = cancellation; }
        private void Tick(int depth = 0)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (++_steps > _options.MaxSteps || depth > _options.MaxDepth) throw new ExecutionLimitException("contract-budget");
        }
        private void Error(List<ContractViolation> errors, string path, string keyword, string? expected = null, string? actual = null)
        { if (errors.Count < _options.MaxErrors) errors.Add(new(path, keyword, expected, actual)); }
        private bool Match(string pattern, string text)
        {
            Tick();
            try { return _patterns[pattern].IsMatch(text); }
            catch (RegexMatchTimeoutException) { throw new ExecutionLimitException("contract-regex"); }
        }
        private bool Probe(JsonElement schema, JsonElement data, string path, int depth) => Evaluate(schema, data, path, depth, new());
        internal bool Evaluate(JsonElement schema, JsonElement data, string path, int depth, List<ContractViolation> errors)
        {
            Tick(depth);
            if (schema.ValueKind == JsonValueKind.True) return true;
            if (schema.ValueKind == JsonValueKind.False) { Error(errors, path, "false"); return false; }
            bool valid = true;
            void Fail(string key, string? expected = null) { valid = false; Error(errors, path, key, expected, Kind(data)); }
            bool Apply(JsonElement child, JsonElement input, string pointer) => Evaluate(child, input, pointer, depth + 1, errors);
            if (schema.TryGetProperty("$ref", out var reference)) valid &= Apply(JsonSchemaContractProvider.Resolve(_root, reference.GetString()!), data, path);
            if (schema.TryGetProperty("type", out var type))
            {
                var names = type.ValueKind == JsonValueKind.Array ? type.EnumerateArray().Select(x => x.GetString()!).ToArray() : new[] { type.GetString()! };
                if (!names.Any(n => HasType(data, n))) Fail("type", string.Join("|", names));
            }
            if (schema.TryGetProperty("const", out var constant) && !Equal(data, constant, 1)) Fail("const");
            if (schema.TryGetProperty("enum", out var alternatives) && !alternatives.EnumerateArray().Any(x => Equal(data, x, 1))) Fail("enum");
            foreach (string keyword in new[] { "allOf", "anyOf", "oneOf" })
            {
                if (!schema.TryGetProperty(keyword, out var choices)) continue;
                int matches = 0;
                foreach (var choice in choices.EnumerateArray())
                {
                    if (keyword == "allOf") valid &= Apply(choice, data, path);
                    else if (Probe(choice, data, path, depth + 1)) matches++;
                }
                if (keyword == "anyOf" && matches == 0 || keyword == "oneOf" && matches != 1) Fail(keyword);
            }
            if (schema.TryGetProperty("not", out var not) && Probe(not, data, path, depth + 1)) Fail("not");
            if (schema.TryGetProperty("if", out var condition))
            {
                string branch = Probe(condition, data, path, depth + 1) ? "then" : "else";
                if (schema.TryGetProperty(branch, out var child)) valid &= Apply(child, data, path);
            }
            switch (data.ValueKind)
            {
                case JsonValueKind.Object:
                {
                    var members = data.EnumerateObject().ToArray();
                    if (schema.TryGetProperty("minProperties", out var min) && members.Length < min.GetInt32()) Fail("minProperties");
                    if (schema.TryGetProperty("maxProperties", out var max) && members.Length > max.GetInt32()) Fail("maxProperties");
                    if (schema.TryGetProperty("required", out var required))
                        foreach (var name in required.EnumerateArray())
                            if (!data.TryGetProperty(name.GetString()!, out _))
                            { valid = false; Error(errors, path + "/" + Escape(name.GetString()!), "required"); }
                    bool hasProperties = schema.TryGetProperty("properties", out var properties);
                    bool hasPatterns = schema.TryGetProperty("patternProperties", out var patterns);
                    foreach (var member in members)
                    {
                        Tick(); string pointer = path + "/" + Escape(member.Name); bool covered = false;
                        if (hasProperties && properties.TryGetProperty(member.Name, out var child))
                        { covered = true; valid &= Apply(child, member.Value, pointer); }
                        if (hasPatterns)
                            foreach (var pattern in patterns.EnumerateObject())
                                if (Match(pattern.Name, member.Name)) { covered = true; valid &= Apply(pattern.Value, member.Value, pointer); }
                        if (!covered && schema.TryGetProperty("additionalProperties", out var additional)) valid &= Apply(additional, member.Value, pointer);
                        if (schema.TryGetProperty("propertyNames", out var names))
                        {
                            using var name = JsonDocument.Parse(JsonSerializer.Serialize(member.Name));
                            valid &= Apply(names, name.RootElement, pointer);
                        }
                    }
                    if (schema.TryGetProperty("dependentRequired", out var dependencies))
                        foreach (var dependency in dependencies.EnumerateObject())
                            if (data.TryGetProperty(dependency.Name, out _))
                                foreach (var name in dependency.Value.EnumerateArray())
                                    if (!data.TryGetProperty(name.GetString()!, out _))
                                    { valid = false; Error(errors, path + "/" + Escape(name.GetString()!), "dependentRequired"); }
                    if (schema.TryGetProperty("dependentSchemas", out var schemas))
                        foreach (var dependency in schemas.EnumerateObject())
                            if (data.TryGetProperty(dependency.Name, out _)) valid &= Apply(dependency.Value, data, path);
                    break;
                }
                case JsonValueKind.Array:
                {
                    int length = data.GetArrayLength();
                    if (schema.TryGetProperty("minItems", out var min) && length < min.GetInt32()) Fail("minItems");
                    if (schema.TryGetProperty("maxItems", out var max) && length > max.GetInt32()) Fail("maxItems");
                    int prefixLength = schema.TryGetProperty("prefixItems", out var prefix) ? prefix.GetArrayLength() : 0;
                    bool hasItems = schema.TryGetProperty("items", out var items);
                    for (int i = 0; i < length; i++)
                    {
                        Tick(); string pointer = path + "/" + i.ToString(CultureInfo.InvariantCulture);
                        if (i < prefixLength) valid &= Apply(prefix[i], data[i], pointer);
                        else if (hasItems) valid &= Apply(items, data[i], pointer);
                    }
                    if (schema.TryGetProperty("contains", out var contains))
                    {
                        int count = 0;
                        foreach (var item in data.EnumerateArray()) if (Probe(contains, item, path, depth + 1)) count++;
                        int minimum = schema.TryGetProperty("minContains", out var lower) ? lower.GetInt32() : 1;
                        int maximum = schema.TryGetProperty("maxContains", out var upper) ? upper.GetInt32() : int.MaxValue;
                        if (count < minimum || count > maximum) Fail("contains");
                    }
                    if (schema.TryGetProperty("uniqueItems", out var unique) && unique.GetBoolean())
                    {
                        bool duplicate = false;
                        for (int i = 0; i < length && !duplicate; i++) for (int j = 0; j < i && !duplicate; j++) duplicate = Equal(data[i], data[j], 1);
                        if (duplicate) Fail("uniqueItems");
                    }
                    break;
                }
                case JsonValueKind.String:
                {
                    string text = data.GetString()!; int length = text.EnumerateRunes().Count();
                    if (schema.TryGetProperty("minLength", out var min) && length < min.GetInt32()) Fail("minLength");
                    if (schema.TryGetProperty("maxLength", out var max) && length > max.GetInt32()) Fail("maxLength");
                    if (schema.TryGetProperty("pattern", out var pattern) && !Match(pattern.GetString()!, text)) Fail("pattern");
                    break;
                }
                case JsonValueKind.Number:
                {
                    JsonNumber number = JsonNumber.Parse(data.GetRawText());
                    foreach (string key in new[] { "minimum", "maximum", "exclusiveMinimum", "exclusiveMaximum", "multipleOf" })
                    {
                        if (!schema.TryGetProperty(key, out var bound)) continue;
                        JsonNumber limit = JsonNumber.Parse(bound.GetRawText()); int comparison = number.Compare(limit);
                        if (key == "minimum" && comparison < 0 || key == "maximum" && comparison > 0 ||
                            key == "exclusiveMinimum" && comparison <= 0 || key == "exclusiveMaximum" && comparison >= 0 ||
                            key == "multipleOf" && !number.IsMultipleOf(limit)) Fail(key);
                    }
                    break;
                }
            }
            return valid;
        }
        private bool Equal(JsonElement left, JsonElement right, int depth)
        {
            Tick(depth);
            if (left.ValueKind != right.ValueKind) return false;
            switch (left.ValueKind)
            {
                case JsonValueKind.Number: return JsonNumber.Parse(left.GetRawText()).Compare(JsonNumber.Parse(right.GetRawText())) == 0;
                case JsonValueKind.String: return left.GetString() == right.GetString();
                case JsonValueKind.Object:
                    var properties = left.EnumerateObject().ToArray();
                    return properties.Length == right.EnumerateObject().Count() && properties.All(p => right.TryGetProperty(p.Name, out var value) && Equal(p.Value, value, depth + 1));
                case JsonValueKind.Array:
                    if (left.GetArrayLength() != right.GetArrayLength()) return false;
                    for (int i = 0; i < left.GetArrayLength(); i++) if (!Equal(left[i], right[i], depth + 1)) return false;
                    return true;
                default: return true;
            }
        }
        private static bool HasType(JsonElement value, string name) => name == "integer"
            ? value.ValueKind == JsonValueKind.Number && JsonNumber.Parse(value.GetRawText()).IsInteger : Kind(value) == name;
        private static string Kind(JsonElement value) => value.ValueKind switch
        {
            JsonValueKind.True or JsonValueKind.False => "boolean", JsonValueKind.Null => "null", JsonValueKind.Number => "number",
            JsonValueKind.String => "string", JsonValueKind.Array => "array", JsonValueKind.Object => "object", _ => "undefined"
        };
        private static string Escape(string name) => name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
    }
}
