using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Text.Json;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.Objects;

internal static class AuthoringTests
{
    internal sealed class NestedSource
    {
        public NestedCustomer Customer { get; } = new();
        public int Unrelated => throw new InvalidOperationException("Irrelevant getter invoked.");
    }
    internal sealed class NestedCustomer
    {
        public string Name => "Ada";
        public string Secret => throw new InvalidOperationException("Secret getter invoked.");
    }
    internal sealed class SimpleSource { public int Value { get; } = 7; }
    internal sealed class SimpleTarget { public int Answer { get; init; } }
    internal sealed class RequiredTarget { public required string Name { get; init; } }

    internal static void Register(Action<string, Action> test)
    {
        var projector = new ObjectProjector();
        CompiledProjection Plan(string id, int version, string expression) =>
            projector.Compile(new ProjectionDefinition { Id = id, Version = version, Expression = expression });

        test("preflight: static member is known without calling getter", () =>
        {
            var report = new ProjectionInspector(projector).Inspect(new() { Expression = "Value" }, typeof(SimpleSource));
            Check.True(report.Success);
            Check.True(report.Explanation.CanSelectRootMembers);
            Check.True(report.Explanation.Paths.Any(x => x == "/Value"));
        });
        test("preflight: unknown source member is reported", () =>
        {
            var report = new ProjectionInspector(projector).Inspect(new() { Expression = "Absent" }, typeof(SimpleSource));
            Check.True(report.Success); // Missing JSONata members can be handled by a rule fallback.
            Check.True(report.Issues.Any(x => x.Code == "PX_SOURCE_MEMBER" && x.Severity == ProjectionIssueSeverity.Warning));
        });
        test("preflight: target unknown field is reported for explicit fields", () =>
        {
            var report = new ProjectionInspector(projector).Inspect(new()
            { Fields = new() { ["NotAnswer"] = "Value" } }, typeof(SimpleSource), typeof(SimpleTarget));
            Check.True(!report.Success);
            Check.True(report.Issues.Any(x => x.Code == "PX_TARGET_FIELD"));
        });
        test("preflight: required target member must be present for explicit fields", () =>
        {
            var report = new ProjectionInspector(projector).Inspect(new()
            { Fields = new() { ["Extra"] = "'x'" } }, targetType: typeof(RequiredTarget));
            Check.True(report.Issues.Any(x => x.Code == "PX_TARGET_REQUIRED"));
        });
        test("preflight: arbitrary expression cannot prove destination shape", () =>
        {
            var report = new ProjectionInspector(projector).Inspect(new()
            { Expression = "{'Name':'Ada'}" }, targetType: typeof(RequiredTarget));
            Check.True(report.Issues.Any(x => x.Code == "PX_TARGET_INDETERMINATE"));
            Check.True(report.Success);
        });
        test("preflight: invalid expression yields diagnostic, not runtime source access", () =>
        {
            var report = new ProjectionInspector(projector).Inspect(new() { Expression = "1 +" }, typeof(NestedSource));
            Check.True(!report.Success);
            Check.True(report.Issues.Any(x => x.Code == "PX_COMPILE"));
        });
        test("preflight: full-read reason is explicit", () =>
        {
            var p = projector.Compile(new() { Expression = "*" });
            var report = new ProjectionInspector(projector).Explain(p);
            Check.True(!report.CanSelectRootMembers);
            Check.True(!string.IsNullOrWhiteSpace(report.FallbackReason));
        });
        test("registry: staging does not activate and activation is atomic", () =>
        {
            var rules = new ProjectionRuleRegistry(projector);
            var first = rules.Stage(new() { Id = "customer", Version = 1, Expression = "a" });
            Check.True(rules.GetActive("customer") is null);
            Check.True(rules.TryActivate("customer", 1, expectedActiveVersion: null));
            Check.Equal(first.Fingerprint, rules.GetActive("customer")!.Fingerprint);
            rules.Stage(new() { Id = "customer", Version = 2, Expression = "b" });
            Check.True(!rules.TryActivate("customer", 2, expectedActiveVersion: null));
            Check.Equal(1, rules.GetActive("customer")!.Version);
            Check.True(rules.TryActivate("customer", 2, expectedActiveVersion: 1));
            Check.Equal(2, rules.GetActive("customer")!.Version);
            Check.True(rules.TryActivate("customer", 1, expectedActiveVersion: 2));
            Check.Equal(1, rules.GetActive("customer")!.Version);
        });
        test("registry: failed compile does not install rule", () =>
        {
            var rules = new ProjectionRuleRegistry(projector);
            Check.Throws<ProjectionConfigurationException>(() => rules.Stage(new() { Id = "broken", Version = 1, Expression = "1 +" }));
            Check.True(rules.GetActive("broken") is null);
            Check.True(rules.ListVersions("broken").Count == 0);
        });
        test("registry: same version cannot be silently overwritten", () =>
        {
            var rules = new ProjectionRuleRegistry(projector);
            rules.Stage(new() { Id = "stable", Version = 1, Expression = "a" });
            Check.Throws<ProjectionConfigurationException>(() => rules.Stage(new() { Id = "stable", Version = 1, Expression = "b" }));
        });
        test("registry: mutable source dictionary does not affect staged definition", () =>
        {
            var rules = new ProjectionRuleRegistry(projector);
            var fields = new Dictionary<string, string> { ["Answer"] = "Value" };
            rules.Stage(new() { Id = "fields", Version = 1, Fields = fields });
            fields["Answer"] = "2";
            Check.True(rules.TryActivate("fields", 1, null));
            Check.Json("{\"Answer\":7}", projector.ProjectToToken(new SimpleSource(), rules.GetActive("fields")!));
        });
        test("registry: bounded number of versions", () =>
        {
            var rules = new ProjectionRuleRegistry(projector, maxRules: 1, maxVersionsPerRule: 2);
            rules.Stage(new() { Id = "only", Version = 1, Expression = "1" });
            rules.Stage(new() { Id = "only", Version = 2, Expression = "2" });
            Check.Throws<ProjectionConfigurationException>(() => rules.Stage(new() { Id = "only", Version = 3, Expression = "3" }));
            Check.Throws<ProjectionConfigurationException>(() => rules.Stage(new() { Id = "other", Version = 1, Expression = "1" }));
        });
        test("registry: only one compare-and-swap winner", () =>
        {
            var rules = new ProjectionRuleRegistry(projector);
            rules.Stage(new() { Id = "race", Version = 1, Expression = "1" });
            rules.Stage(new() { Id = "race", Version = 2, Expression = "2" });
            rules.Stage(new() { Id = "race", Version = 3, Expression = "3" });
            Check.True(rules.TryActivate("race", 1, null));
            int successes = 0;
            System.Threading.Tasks.Parallel.For(2, 4, i =>
            { if (rules.TryActivate("race", i, expectedActiveVersion: 1)) System.Threading.Interlocked.Increment(ref successes); });
            Check.Equal(1, successes);
        });
        test("comparison: successful report includes both rule identities", () =>
        {
            var report = new ProjectionComparer(projector).Compare(new { a = 1 },
                Plan("before", 3, "{'x':a}"), Plan("after", 4, "{'x':a*2}"));
            Check.Equal("before", report.BeforeRuleId); Check.Equal(3, report.BeforeVersion);
            Check.Equal("after", report.AfterRuleId); Check.Equal(4, report.AfterVersion);
            Check.True(report.BeforeSucceeded && report.AfterSucceeded && !report.Equivalent);
            Check.True(report.Differences.Any(x => x.JsonPointer == "/x"));
            Check.True(report.Differences.All(x => !x.ToString()!.Contains("secret-value", StringComparison.Ordinal)));
        });
        test("comparison: unchanged rules report equivalent with identities", () =>
        {
            var report = new ProjectionComparer(projector).Compare(new { a = 1 }, Plan("a", 1, "a"), Plan("b", 2, "a"));
            Check.True(report.Equivalent);
            Check.Equal("a", report.BeforeRuleId); Check.Equal("b", report.AfterRuleId);
        });
        test("comparison: missing field and null are different", () =>
        {
            var report = new ProjectionComparer(projector).Compare(new { a = 1 },
                Plan("a", 1, "{'x':null}"), Plan("b", 2, "{'y':1}"));
            Check.True(report.Differences.Any(x => x.JsonPointer == "/x" && x.Kind == ProjectionDifferenceKind.Removed));
            Check.True(report.Differences.Any(x => x.JsonPointer == "/y" && x.Kind == ProjectionDifferenceKind.Added));
        });
        test("comparison: differences honor the configured limit", () =>
        {
            var report = new ProjectionComparer(projector).Compare(null,
                Plan("x", 1, "{'a':1,'b':2,'c':3}"), Plan("y", 2, "{'a':4,'b':5,'c':6}"), maxDifferences: 1);
            Check.Equal(1, report.Differences.Count);
            Check.True(report.Truncated && !report.Equivalent);
        });
        test("comparison: evaluation error retains metadata", () =>
        {
            var report = new ProjectionComparer(projector).Compare(null,
                Plan("working", 8, "1"), Plan("failing", 9, "1 / 0"));
            Check.Equal("working", report.BeforeRuleId); Check.Equal(8, report.BeforeVersion);
            Check.Equal("failing", report.AfterRuleId); Check.Equal(9, report.AfterVersion);
            Check.True(report.BeforeSucceeded && !report.AfterSucceeded);
            Check.True(!report.Equivalent);
        });
        test("output policy: omit object null fields but preserve array slots", () =>
        {
            var p = new ObjectProjector(new() { NullProperties = ProjectionNullProperties.Omit });
            var output = p.ProjectToToken(null, new ProjectionDefinition { Expression = "{'a':null,'b':[null,{'c':null,'d':1}]}" });
            Check.Json("{\"b\":[null,{\"d\":1}]}", output);
        });
        test("output policy: default preserves null", () =>
            Check.Json("{\"a\":null}", projector.ProjectToToken(null, new ProjectionDefinition { Expression = "{'a':null}" })));
        test("selective: nested POCO paths do not invoke irrelevant getters", () =>
        {
            var p = new ObjectProjector(new() { EnableSelectiveReading = true });
            var plan = p.Compile(new() { Expression = "Customer.Name" });
            Check.True(plan.Dependencies.PropertyPaths.Any(x => x.SequenceEqual(new[] { "Customer", "Name" })));
            Check.Equal("Ada", p.Project<string>(new NestedSource(), plan));
        });
        test("selective: requesting parent reads complete nested object", () =>
        {
            var p = new ObjectProjector(new() { EnableSelectiveReading = true });
            Check.Throws<ObjectReadException>(() => p.ProjectToToken(new NestedSource(), new ProjectionDefinition { Expression = "Customer" }));
        });
        test("exposure: root allowlist filters POCO, dictionary, JSON DOM", () =>
        {
            var options = new ObjectReadOptions { AllowedRootMembers = new[] { "Value" } };
            var reader = new ObjectReader(options);
            Check.Json("{\"Value\":7}", reader.Read(new { Value = 7, Hidden = 9 }));
            Check.Json("{\"Value\":7}", reader.Read(new Dictionary<string, int> { ["Value"] = 7, ["Hidden"] = 9 }));
            using var document = JsonDocument.Parse("{\"Value\":7,\"Hidden\":9}");
            Check.Json("{\"Value\":7}", reader.Read(document));
            Check.Json("{\"Value\":7}", reader.Read(JToken.Parse("{\"Value\":7,\"Hidden\":9}")));
        });
        test("exposure: root allowlist also applies to each array item", () =>
        {
            var reader = new ObjectReader(new() { AllowedRootMembers = new[] { "Value" } });
            Check.Json("[{\"Value\":7},{\"Value\":8}]", reader.Read(new[] { new { Value = 7, Hidden = 9 }, new { Value = 8, Hidden = 10 } }));
            Check.Json("[{\"Value\":7}]", reader.Read(JToken.Parse("[{\"Value\":7,\"Hidden\":9}]")));
        });
        test("exposure: allowlist prevents hostile root getter", () =>
        {
            var p = new ObjectProjector(new() { Source = new() { AllowedRootMembers = new[] { "Value" } } });
            Check.Equal(7, p.Project<int>(new { Value = 7, Hidden = new object() }, new ProjectionDefinition { Expression = "Value" }));
        });
        test("replay identity: output policy changes profile fingerprint", () =>
        {
            var p = Plan("rule", 1, "1");
            var a = ProjectionReplayIdentity.Create(p, new ProjectionOptions());
            var b = ProjectionReplayIdentity.Create(p, new ProjectionOptions { NullProperties = ProjectionNullProperties.Omit });
            Check.True(a.ConfigurationFingerprint != b.ConfigurationFingerprint);
        });
        test("replay identity: exposure and bindings policy change profile fingerprint", () =>
        {
            var p = Plan("rule", 1, "1");
            var a = ProjectionReplayIdentity.Create(p, new ProjectionOptions());
            var b = ProjectionReplayIdentity.Create(p, new ProjectionOptions { Source = new() { AllowedRootMembers = new[] { "Visible" } } });
            var c = ProjectionReplayIdentity.Create(p, new ProjectionOptions { Bindings = new() { AllowedRootMembers = new[] { "factor" } } });
            Check.True(a.ConfigurationFingerprint != b.ConfigurationFingerprint);
            Check.True(a.ConfigurationFingerprint != c.ConfigurationFingerprint);
            Check.True(b.ConfigurationFingerprint != c.ConfigurationFingerprint);
        });
        test("configuration: external mutable exposure collections are snapshotted", () =>
        {
            var allowed = new List<string> { "Value" };
            var p = new ObjectProjector(new() { Source = new() { AllowedRootMembers = allowed } });
            var compiled = p.Compile(new() { Expression = "Value" });
            var before = ProjectionReplayIdentity.Create(compiled, p.Options).ConfigurationFingerprint;
            allowed.Clear(); allowed.Add("Hidden");
            Check.Equal(before, ProjectionReplayIdentity.Create(compiled, p.Options).ConfigurationFingerprint);
            Check.Json("{\"Value\":7}", new ObjectReader(p.Options.Source).Read(new { Value = 7, Hidden = 99 }));
            Check.Equal(7, p.Project<int>(new { Value = 7, Hidden = 99 }, compiled));
        });
        test("replay identity: source values need explicit provenance", () =>
        {
            var id = ProjectionReplayIdentity.Create(Plan("rule", 1, "1"), new ProjectionOptions());
            Check.True(!id.CanReproduce);
            Check.True(id.Limitations.Contains("source-values-unpinned"));
            var withSource = ProjectionReplayIdentity.Create(Plan("rule", 1, "1"), new ProjectionOptions(), sourceIdentity: "input-snapshot-v1");
            Check.True(withSource.CanReproduce);
        });
        test("replay identity: variable values require external provenance", () =>
        {
            var p = Plan("rule", 1, "1");
            var context = new ProjectionExecutionContext(new Dictionary<string, object?> { ["factor"] = 7 });
            var identity = ProjectionReplayIdentity.Create(p, new ProjectionOptions(), context);
            Check.True(!identity.CanReproduce);
            Check.True(identity.Limitations.Any(x => x == "binding-values-unpinned"));
        });
        test("telemetry: compilation, source and materialization stages are instrumented", () =>
        {
            var observed = new HashSet<string>();
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, l) =>
            { if (instrument.Meter.Name == ProjectionTelemetry.Name) l.EnableMeasurementEvents(instrument); };
            listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
            {
                if (instrument.Name == "projecta.stage.duration")
                    foreach (var tag in tags) if (tag.Key == "stage" && tag.Value is string stage) lock (observed) observed.Add(stage);
            });
            listener.Start();
            var plan = projector.Compile(new() { Expression = "Value" });
            Check.Equal(7, projector.Project<int>(new SimpleSource(), plan));
            Check.True(observed.Contains("source")); Check.True(observed.Contains("materialize"));
            Check.True(observed.Contains("compile"));
        });
    }
}
