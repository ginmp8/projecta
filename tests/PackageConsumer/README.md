# Packaged-consumer verification

This intentionally standalone project restores the built local packages rather than project references. It is excluded from both source solutions to avoid a build/package cycle. The bounded consumer fixture extends central package management with self-package versions taken from the inherited release `Version`.

After verification and packaging:

```sh
dotnet restore tests/PackageConsumer/Consumer.csproj --configfile tests/PackageConsumer/NuGet.Config --packages artifacts/consumer-cache --force
dotnet run --project tests/PackageConsumer/Consumer.csproj -c Release --no-restore
```

Use an empty `artifacts/consumer-cache` for a new unpublished candidate to avoid reusing packages with stale versions. Never replace an already published package in place. The fixture verifies 22 checks covering runtime mapping, contracts, functions, metadata-driven deserialization, documents, JSONL, LINQ, an actual analyzer-generated mapper, static rule explanations, staged atomic activation, version comparison, replay provenance and null-property policies. It is not a SQL-provider or Native AOT test.
