using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Projecta.Analysis;

namespace Projecta.Mapping;

/// <summary>A frozen, reusable plan. Per-call variables, authorization and cancellation never live in the plan.</summary>
public sealed class CompiledProjection
{
    internal ProjectaQuery Query { get; }
    internal IProjectionContract? InputContract { get; }
    internal IProjectionContract? OutputContract { get; }
    private readonly (string Name, int Start, int End)[] _fields;
    public string Id { get; }
    public int Version { get; }
    public string? TargetType { get; }
    public string Fingerprint { get; }
    public QueryDependencyAnalysis Dependencies { get; }

    internal CompiledProjection(ProjectionDefinition definition, ProjectaQuery query,
        IProjectionContract? inputContract, IProjectionContract? outputContract)
    {
        Id = definition.Id; Version = definition.Version; TargetType = definition.TargetType; Query = query;
        InputContract = inputContract; OutputContract = outputContract;
        Dependencies = QueryDependencyAnalysis.Analyze(query);
        _fields = FieldSpans(definition);
        Fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            definition.BuildExpression() + "\n" + definition.TargetType + "\n" + definition.InputSchema?.GetRawText() + "\n" + definition.OutputSchema?.GetRawText())));
    }

    internal string? FindField(int? position) => FindField(_fields, position);
    internal static string? FindField(ProjectionDefinition definition, int? position) => FindField(FieldSpans(definition), position);
    private static string? FindField((string Name, int Start, int End)[] spans, int? position)
    {
        if (position is null) return null;
        foreach (var field in spans) if (position >= field.Start && position <= field.End) return field.Name;
        return null;
    }
    private static (string Name, int Start, int End)[] FieldSpans(ProjectionDefinition definition)
    {
        if (definition.Fields is null || !string.IsNullOrWhiteSpace(definition.Expression))
            return Array.Empty<(string, int, int)>();
        int offset = 1;
        var spans = new List<(string, int, int)>();
        foreach (var pair in definition.Fields.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (spans.Count > 0) offset++;
            offset += JsonSerializer.Serialize(pair.Key).Length + 2;
            spans.Add((pair.Key, offset, offset + (pair.Value?.Length ?? 0) + 1));
            offset += (pair.Value?.Length ?? 0) + 1;
        }
        return spans.ToArray();
    }
}
