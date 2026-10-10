using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using Projecta.Execution;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.Objects;
using Projecta.SystemTextJson;

namespace Projecta.Tooling;

public sealed record SemanticCaseResult(string Id, bool Passed, string Outcome, string? ErrorCode);
public sealed record SemanticSuiteResult(string Name, string Sha256, string EngineVersion, int Total, int Passed, int Failed, int Skipped,
    IReadOnlyList<SemanticCaseResult> Cases);

/// <summary>Runs caller-owned, explicit expected-value fixtures. Passing a finite suite is not full language conformance.</summary>
public static class SemanticSuite
{
    public static SemanticSuiteResult Run(JsonElement suite, CancellationToken cancellationToken = default)
    {
        if (suite.ValueKind != JsonValueKind.Object || !suite.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String ||
            !suite.TryGetProperty("cases", out var cases) || cases.ValueKind != JsonValueKind.Array || cases.GetArrayLength() is < 1 or > 10_000)
            throw new ProjectionConfigurationException("A named, nonempty bounded semantic suite is required.");
        var ids = new HashSet<string>(StringComparer.Ordinal); var results = new List<SemanticCaseResult>();
        var reader = new ObjectReader(new() { MaxNodes = 100_000, MaxDepth = 64, MaxCharacters = 1_000_000 });
        foreach (var test in cases.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (test.ValueKind != JsonValueKind.Object || !test.TryGetProperty("id", out var idValue) || idValue.ValueKind != JsonValueKind.String ||
                !test.TryGetProperty("expression", out var expression) || expression.ValueKind != JsonValueKind.String || !test.TryGetProperty("input", out var input))
                throw new ProjectionConfigurationException("Semantic case requires id, expression and input.");
            string id = idValue.GetString()!;
            if (id.Length == 0 || !ids.Add(id)) throw new ProjectionConfigurationException("Case identities must be unique and nonempty.");
            bool expectValue = test.TryGetProperty("expected", out var expected);
            bool expectUndefined = test.TryGetProperty("undefined", out var undefined);
            bool expectError = test.TryGetProperty("errorCode", out var error);
            if ((expectValue ? 1 : 0) + (expectUndefined ? 1 : 0) + (expectError ? 1 : 0) != 1 ||
                expectUndefined && undefined.ValueKind != JsonValueKind.True || expectError && error.ValueKind != JsonValueKind.String)
                throw new ProjectionConfigurationException("A case requires exactly one expected value, undefined=true, or errorCode.");
            try
            {
                var environment = new EvaluationEnvironment();
                if (test.TryGetProperty("bindings", out var bindings))
                {
                    if (reader.Read(bindings, cancellationToken) is not JObject variables) throw new ProjectionConfigurationException("Bindings must be an object.");
                    foreach (var pair in variables.Properties) environment.BindValue(pair.Key, pair.Value);
                }
                JToken actual = new ProjectaQuery(expression.GetString()!).Eval(reader.Read(input, cancellationToken), environment,
                    new EvaluationLimits { Timeout = TimeSpan.FromSeconds(1), MaxSteps = 200_000, MaxRangeLength = 20_000 }, cancellationToken);
                bool equal;
                if (actual.Type == JTokenType.Undefined) equal = expectUndefined;
                else
                {
                    using var actualJson = JsonDocument.Parse(NativeJsonWriter.ToUtf8Bytes(actual));
                    equal = expectValue && JsonElement.DeepEquals(actualJson.RootElement, expected);
                }
                results.Add(new(id, equal, equal ? "matched" : "value-mismatch", null));
            }
            catch (JsonataException ex)
            { bool match = expectError && error.GetString() == ex.Code.ToString(); results.Add(new(id, match, match ? "matched-error" : "unexpected-error", ex.Code.ToString())); }
            catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
            { results.Add(new(id, false, "execution-failed", ex.GetType().Name)); }
        }
        int passed = results.Count(x => x.Passed);
        return new(name.GetString()!, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(suite.GetRawText()))),
            typeof(ProjectaQuery).Assembly.GetName().Version?.ToString() ?? "unknown", results.Count, passed, results.Count - passed, 0, results.AsReadOnly());
    }
}
