using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Projecta.Contracts;
using Projecta.Documents;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.Objects;
using Projecta.Streaming;
using Projecta.SystemTextJson;

namespace Projecta.Tooling;

public static class ProjectionCli
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args); ArgumentNullException.ThrowIfNull(output); ArgumentNullException.ThrowIfNull(error);
        try
        {
            if (args.Length == 0 || args is ["--help"] or ["help"])
            {
                await output.WriteLineAsync("projecta validate --rule FILE\nprojecta explain --rule FILE\nprojecta compare --before RULE --after RULE --input FILE\nprojecta preview --rule FILE --input FILE\nprojecta jsonl --rule FILE --input FILE\nprojecta pipeline --definition FILE --input FILE\nprojecta patch --patch FILE --input FILE\nprojecta conformance --suite FILE\nprojecta benchmark [--iterations N --warmup N --rounds N]").ConfigureAwait(false); return 0;
            }
            string command = args[0]; var flags = ParseFlags(args);
            string Required(string key) => flags.TryGetValue(key, out var value) ? value : throw new ProjectionConfigurationException("Missing option.");
            int Number(string key, int fallback) => flags.TryGetValue(key, out var value) && int.TryParse(value, out var number) ? number : flags.ContainsKey(key) ? throw new ProjectionConfigurationException("Invalid numeric option.") : fallback;
            string[] allowed = command switch
            {
                "validate" or "explain" => new[] { "--rule" }, "preview" or "jsonl" => new[] { "--rule", "--input" },
                "compare" => new[] { "--before", "--after", "--input" },
                "pipeline" => new[] { "--definition", "--input" }, "patch" => new[] { "--patch", "--input" },
                "conformance" => new[] { "--suite" }, "benchmark" => new[] { "--iterations", "--warmup", "--rounds" },
                _ => throw new ProjectionConfigurationException("Unknown command.")
            };
            foreach (string flag in flags.Keys) if (Array.IndexOf(allowed, flag) < 0) throw new ProjectionConfigurationException("Unknown option.");
            var projector = new ObjectProjector(new() { ContractProvider = new JsonSchemaContractProvider() });
            if (command == "compare")
            {
                var before = ProjectionDefinition.Parse(await ReadText(Required("--before"), 65_536, cancellationToken).ConfigureAwait(false));
                var after = ProjectionDefinition.Parse(await ReadText(Required("--after"), 65_536, cancellationToken).ConfigureAwait(false));
                var first = projector.TryCompile(before, cancellationToken);
                var second = projector.TryCompile(after, cancellationToken);
                if (!first.Success || !second.Success)
                {
                    await error.WriteLineAsync(JsonSerializer.Serialize(new { Before = first.Diagnostics, After = second.Diagnostics })).ConfigureAwait(false);
                    return 2;
                }
                using var compareData = JsonDocument.Parse(await ReadBytes(Required("--input"), 8_000_000, cancellationToken).ConfigureAwait(false));
                var report = new ProjectionComparer(projector).Compare(compareData.RootElement, first.Value!, second.Value!, cancellationToken: cancellationToken);
                // Output keys can be computed from input values. Never write their pointers to CLI output.
                await output.WriteLineAsync(JsonSerializer.Serialize(new
                {
                    report.BeforeRuleId, report.BeforeVersion, report.BeforeFingerprint, report.BeforeSucceeded,
                    report.AfterRuleId, report.AfterVersion, report.AfterFingerprint, report.AfterSucceeded,
                    report.Equivalent, report.Truncated,
                    PathsRedacted = true,
                    Differences = report.Differences.Select(x => new { x.Kind }).ToArray(),
                    report.BeforeDiagnostics, report.AfterDiagnostics
                })).ConfigureAwait(false);
                return report.BeforeSucceeded && report.AfterSucceeded ? 0 : 1;
            }
            if (command is "validate" or "explain" or "preview" or "jsonl")
            {
                var definition = ProjectionDefinition.Parse(await ReadText(Required("--rule"), 65_536, cancellationToken).ConfigureAwait(false));
                var compiled = projector.TryCompile(definition, cancellationToken);
                if (!compiled.Success) { await error.WriteLineAsync(JsonSerializer.Serialize(compiled.Diagnostics)).ConfigureAwait(false); return 2; }
                var plan = compiled.Value!;
                if (command == "validate")
                { await output.WriteLineAsync(JsonSerializer.Serialize(new { valid = true, plan.Id, plan.Version, plan.Fingerprint, plan.Dependencies })).ConfigureAwait(false); return 0; }
                if (command == "explain")
                { await output.WriteLineAsync(JsonSerializer.Serialize(new ProjectionInspector(projector).Explain(plan))).ConfigureAwait(false); return 0; }
                if (command == "jsonl")
                {
                    await using var stream = File.OpenRead(Required("--input")); int failed = 0;
                    await foreach (var row in projector.ProjectJsonLinesAsync<JToken>(stream, plan, cancellationToken: cancellationToken).ConfigureAwait(false))
                    {
                        if (row.Result.Success) await output.WriteLineAsync(Encoding.UTF8.GetString(NativeJsonWriter.ToUtf8Bytes(row.Result.Value!, cancellationToken: cancellationToken))).ConfigureAwait(false);
                        else { failed++; await error.WriteLineAsync(JsonSerializer.Serialize(new { row.Index, row.LineNumber, row.Result.Diagnostics })).ConfigureAwait(false); }
                    }
                    return failed == 0 ? 0 : 1;
                }
                using var input = JsonDocument.Parse(await ReadBytes(Required("--input"), 8_000_000, cancellationToken).ConfigureAwait(false));
                var result = projector.TryProject<JToken>(input.RootElement, plan, cancellationToken: cancellationToken);
                if (!result.Success) { await error.WriteLineAsync(JsonSerializer.Serialize(result.Diagnostics)).ConfigureAwait(false); return 1; }
                await WriteToken(output, result.Value!, cancellationToken).ConfigureAwait(false); return 0;
            }
            if (command == "conformance")
            {
                using var suite = JsonDocument.Parse(await ReadBytes(Required("--suite"), 16_000_000, cancellationToken).ConfigureAwait(false));
                var result = SemanticSuite.Run(suite.RootElement, cancellationToken);
                await output.WriteLineAsync(JsonSerializer.Serialize(result)).ConfigureAwait(false); return result.Failed == 0 ? 0 : 1;
            }
            if (command == "benchmark")
            {
                var result = ProjectionBenchmarks.Run(Number("--iterations", 2000), Number("--warmup", 200), Number("--rounds", 9), cancellationToken);
                await output.WriteLineAsync(JsonSerializer.Serialize(result)).ConfigureAwait(false); return 0;
            }
            using var data = JsonDocument.Parse(await ReadBytes(Required("--input"), 8_000_000, cancellationToken).ConfigureAwait(false));
            JToken source = new ObjectReader().Read(data.RootElement, cancellationToken);
            JToken transformed = command == "patch"
                ? JsonPatchDocument.Parse(await ReadText(Required("--patch"), 1_000_000, cancellationToken).ConfigureAwait(false)).Apply(source, cancellationToken)
                : DocumentPipeline.Parse(await ReadText(Required("--definition"), 1_000_000, cancellationToken).ConfigureAwait(false), projector).Execute(source, cancellationToken: cancellationToken).Value;
            await WriteToken(output, transformed, cancellationToken).ConfigureAwait(false); return 0;
        }
        catch (OperationCanceledException) { await error.WriteLineAsync("{\"code\":\"cancelled\"}").ConfigureAwait(false); return 130; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            string code = ex switch
            {
                ProjectionConfigurationException or JsonException or ArgumentException => "invalid-input",
                IOException or UnauthorizedAccessException => "file-access-failed",
                DocumentTransformException => "document-operation-failed",
                _ => "execution-failed"
            };
            await error.WriteLineAsync(JsonSerializer.Serialize(new { code })).ConfigureAwait(false);
            return code is "invalid-input" or "file-access-failed" ? 2 : 1;
        }
    }
    private static Dictionary<string, string> ParseFlags(string[] args)
    {
        var flags = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 1; i < args.Length; i += 2)
            if (i + 1 == args.Length || !args[i].StartsWith("--", StringComparison.Ordinal) || !flags.TryAdd(args[i], args[i + 1]))
                throw new ProjectionConfigurationException("Options require unique names and values.");
        return flags;
    }
    private static async Task WriteToken(TextWriter output, JToken token, CancellationToken cancellation)
        => await output.WriteLineAsync(Encoding.UTF8.GetString(NativeJsonWriter.ToUtf8Bytes(token, cancellationToken: cancellation))).ConfigureAwait(false);
    private static async Task<string> ReadText(string path, int limit, CancellationToken cancellation)
        => new UTF8Encoding(false, true).GetString(await ReadBytes(path, limit, cancellation).ConfigureAwait(false));
    private static async Task<byte[]> ReadBytes(string path, int limit, CancellationToken cancellation)
    {
        await using var stream = File.OpenRead(path);
        if (stream.Length > limit) throw new ProjectionConfigurationException("File exceeds configured limit.");
        using var result = new MemoryStream(); byte[] buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellation).ConfigureAwait(false)) != 0)
        { if (result.Length + count > limit) throw new ProjectionConfigurationException("File exceeds configured limit."); result.Write(buffer, 0, count); }
        return result.ToArray();
    }
}
