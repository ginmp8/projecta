# Local tooling

`Projecta.Tooling` is both a console project and a packable .NET tool. It uses local files and standard streams. It does not upload inputs, load remote schemas, fetch code or start a server.

From the repository, prefix each command with:

```sh
dotnet run --project src/Projecta.Tooling -c Release --
```

After local tool installation, use `projecta` instead.

| Command | Behavior |
|---|---|
| `validate --rule FILE` | Parse, compile, validate contract support; emit plan metadata |
| `preview --rule FILE --input FILE` | Evaluate one JSON document; emit JSON result |
| `jsonl --rule FILE --input FILE` | Incremental record processing; good rows to stdout, failures to stderr |
| `pipeline --definition FILE --input FILE` | Apply a strict versioned pipeline |
| `patch --patch FILE --input FILE` | Apply a detached document patch |
| `conformance --suite FILE` | Run a caller-supplied finite semantic fixture suite |
| `benchmark --iterations N --warmup N --rounds N` | Run bounded synthetic measurements |

Exit codes: 0 success, 1 execution/record/assertion failure, 2 invalid configuration/input/file access, 130 cancellation. Options must be unique and known. No command overwrites an input file. Shell redirection is controlled by the caller; do not redirect to the same path being read.

Examples:

```sh
dotnet run --project src/Projecta.Tooling -c Release -- validate --rule examples/customer.rule.json
dotnet run --project src/Projecta.Tooling -c Release -- preview --rule examples/customer.rule.json --input examples/customer.json
dotnet run --project src/Projecta.Tooling -c Release -- jsonl --rule examples/customer.rule.json --input examples/customers.jsonl
dotnet run --project src/Projecta.Tooling -c Release -- pipeline --definition examples/normalize.pipeline.json --input examples/customer.json
dotnet run --project src/Projecta.Tooling -c Release -- conformance --suite tests/Projecta.Semantics.Tests/fixtures/semantics.json
dotnet run --project src/Projecta.Tooling -c Release -- benchmark --iterations 2000 --warmup 200 --rounds 9
```

## Semantic fixtures

The format is an explicit expected-outcome corpus:

```json
{
  "name": "application-projections",
  "cases": [
    { "id": "addition", "expression": "a + 1", "input": { "a": 2 }, "expected": 3 },
    { "id": "missing", "expression": "missing", "input": {}, "undefined": true },
    { "id": "type-error", "expression": "a + 1", "input": { "a": "text" }, "errorCode": "T2001" }
  ]
}
```

The suite requires nonempty uniquely identified cases and exactly one expected outcome. It accepts optional bindings for controlled test fixtures, has a case limit and bounded evaluation, hashes the suite content, and reports actual pass/fail/skip denominators. It never promotes a skipped/unrun case into a pass. Test fixtures are trusted developer input, not an authorization policy.

The included 490-case corpus was authored for this project, covering selected semantics. It is **not a complete language conformance corpus or a comparison against another engine**. The tool name `conformance` describes the test purpose, not a certification. Undefined is tested at engine level even though high-level `ObjectProjector` rejects a wholly undefined result.

Preview/pipeline/patch output intentionally contains requested transformed data. Redirect it securely and avoid sensitive fixtures in version control. Diagnostic streams omit raw payloads, filenames and exception stacks.
