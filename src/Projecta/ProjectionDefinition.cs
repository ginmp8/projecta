using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Projecta.Mapping;

public sealed record ProjectionDefinition
{
    public string Id { get; init; } = "projection";
    public int Version { get; init; } = 1;
    public string? TargetType { get; init; }
    public string? Expression { get; init; }
    public Dictionary<string, string>? Fields { get; init; }

    public static ProjectionDefinition Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        if (json.Length > 65_536) throw new ProjectionConfigurationException("Definition exceeds 65536 characters.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        // System.Text.Json otherwise accepts duplicate keys with last-value-wins semantics.
        ValidateDuplicates(document.RootElement);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        var result = JsonSerializer.Deserialize<ProjectionDefinition>(document.RootElement, options)
            ?? throw new ProjectionConfigurationException("Null definition.");
        _ = result.BuildExpression(); return result;
    }

    private static void ValidateDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in value.EnumerateObject())
            { if (!names.Add(p.Name)) throw new ProjectionConfigurationException("Duplicate definition property."); ValidateDuplicates(p.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var child in value.EnumerateArray()) ValidateDuplicates(child);
    }

    internal string BuildExpression()
    {
        if (string.IsNullOrWhiteSpace(Id) || Version < 1) throw new ProjectionConfigurationException("Id and positive version are required.");
        bool full = !string.IsNullOrWhiteSpace(Expression);
        bool fields = Fields is { Count: > 0 };
        if (full == fields) throw new ProjectionConfigurationException("Choose expression OR non-empty fields.");
        if (full) return Expression!;
        var builder = new StringBuilder("{");
        foreach (var pair in Fields!.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value))
                throw new ProjectionConfigurationException("Field names and expressions cannot be empty.");
            if (builder.Length > 1) builder.Append(',');
            builder.Append(JsonSerializer.Serialize(pair.Key)).Append(":(").Append(pair.Value).Append(')');
            if (builder.Length > 32_768) throw new ProjectionConfigurationException("Expression exceeds 32768 characters.");
        }
        return builder.Append('}').ToString();
    }
}

public sealed class ProjectionConfigurationException : Exception
{
    public ProjectionConfigurationException(string message, Exception? inner = null) : base(message, inner) { }
}
public sealed class ProjectionMaterializationException : Exception
{
    public ProjectionMaterializationException(string message, Exception? inner = null) : base(message, inner) { }
}
