# Upstream provenance and preserved scope

Input: `jsonata.net.native-master.zip`, SHA-256:
`e0108b9cdb6d0429f55351d3f149fe695affd4fcbc375e30f01f086f53ef5e76`.

The supplied source identifies version 3.0.0 in its Directory.Build.props. A mutable master archive does not establish an independently verified Git commit; none is invented here.

Projecta retains the complete native engine runtime source (tokenizer, parser, optimizer, AST, evaluator, builtins, function tokens, JSON DOM/parser/conversion) and both supplied JSON bridges. The source is renamed/retargeted and modified directly; the JSONata NuGet engine is not used. The MIT license and original author attribution are preserved.

The original unit tests, bridge tests and helper data are retained under `tests/Projecta.Upstream.Tests`, `tests/Projecta.SystemTextJson.Tests`, `tests/Projecta.NewtonsoftJson.Tests` and `tests/Projecta.TestData`; their namespaces/project files are migrated. The supplied JSON-checker fixtures are retained. These NUnit suites were not executed and some contain compatibility assertions for deliberately changed defaults.

The upstream `jsonata-js` and `json-test-suite` git submodules were empty in the input ZIP. Their complete corpora are **not included or claimed as executed**. Some retained upstream tests still require `jsonata-js/test/test-suite/datasets/library.json`; see the original test paths. No synthetic replacement fixture was used to claim upstream conformance.

The Windows exerciser UI, old benchmark UI/app scaffolding, old signing key, old publishing metadata and repository-specific screenshot/icon assets are not part of this standalone library distribution. They are replaced by a cross-platform console sample, benchmark and new validation/CI scripts. The original runtime engine is not reduced to a partial DSL.
