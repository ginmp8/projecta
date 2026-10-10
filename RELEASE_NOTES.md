# Projecta 0.3.0-alpha.1

This release expands Projecta from dynamic projection into a modular projection/document toolkit without adding an ORM or validation dependency to the core.

**New runtime capabilities:** detached per-execution variables, explicitly allowed functions, structured errors, input/output data contracts, ordered streaming records, document pointers/patches/merge patches and versioned transformation pipelines.

**New static capabilities:** a restricted scalar LINQ Select builder and an incremental mapper generator for .NET 10 compiler hosts. Neither accepts the full dynamic language. Provider SQL and Native AOT remain separately qualified concerns.

**Implementation improvements:** selective root POCO reading, direct UTF-8 destination materialization, numeric fidelity, bounded concurrent compilation outside the cache lock and metadata-only evaluation telemetry.

**Developer tools:** local CLI, finite semantic fixture runner, repeatable synthetic measurements, full/package-free solution entry points, cross-platform verification/pack scripts and configured CI.

See `docs/FEATURES.md` for exact capability boundaries and `docs/VALIDATION.md` for executed versus blocked checks. This source distribution includes optional adapter/test sources whose exact dependency closure could not be restored during validation. Do not treat core success as full integration or production approval.

See `docs/MIGRATION.md` for precision/equality changes and adoption guidance. Package identities were not published or reserved by this delivery. Required inherited license notices remain included.
