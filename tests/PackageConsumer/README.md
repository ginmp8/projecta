# Packaged-consumer verification

This intentionally standalone project restores the built local packages rather than project references. It is excluded from both source solutions to avoid a build/package cycle. The bounded consumer fixture extends central package management with self-package versions taken from the inherited release `Version`.

After verification and packaging:

```sh
dotnet restore tests/PackageConsumer/Consumer.csproj --configfile tests/PackageConsumer/NuGet.Config --packages artifacts/consumer-cache --force
dotnet run --project tests/PackageConsumer/Consumer.csproj -c Release --no-restore
```

Use an empty `artifacts/consumer-cache` for a new unpublished candidate to avoid reusing packages with stale identical prerelease versions. Never replace an already published package in place. The fixture tests runtime mapping, contracts, functions, destination source-generated metadata, documents, JSONL, LINQ and an actual analyzer-generated mapper. It is not a SQL-provider or Native AOT test.
