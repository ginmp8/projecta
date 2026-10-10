using Projecta;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.Objects;
using Projecta.SystemTextJson;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class CoreEvolutionTests
{
    internal static void Register(Action<string, Action> test)
    {
        var projector = new ObjectProjector();
        CompiledProjection Plan(string expression) => projector.Compile(new() { Expression = expression });
        test("context: variables are detached at construction", () =>
        {
            var list = new List<int> { 2, 3 };
            var variables = new Dictionary<string, object?> { ["factor"] = 4, ["numbers"] = list };
            var context = new ProjectionExecutionContext(variables);
            variables["factor"] = 90; list.Add(100);
            Check.Json("20", projector.ProjectToTokenWithContext(null, Plan("$sum($numbers)*$factor"), context));
        });
        test("context: snapshots are cloned per invocation", () =>
        {
            var context = new ProjectionExecutionContext(new Dictionary<string, object?> { ["data"] = new { x = 1 } });
            var plan = Plan("$data");
            ((JObject)projector.ProjectToTokenWithContext(null, plan, context)).Set("x", new JValue(2));
            Check.Json("{\"x\":1}", projector.ProjectToTokenWithContext(null, plan, context));
        });
        foreach (string name in new[] { "sum", "$name", "", "bad-name", "1bad" })
            test("context: rejected variable " + name, () => Check.Throws<ProjectionConfigurationException>(() =>
                new ProjectionExecutionContext(new Dictionary<string, object?> { [name] = 1 })));
        test("context: aggregate binding budget", () => Check.Throws<ObjectReadException>(() => new ProjectionExecutionContext(
            new Dictionary<string, object?> { ["a"] = "abcd", ["b"] = "efgh" }, bindingLimits: new() { MaxCharacters = 8 })));
        test("context: shared plan and isolated concurrent bindings", () =>
        {
            var plan = Plan("value*$factor");
            Parallel.For(0, 300, i =>
            {
                var context = new ProjectionExecutionContext(new Dictionary<string, object?> { ["factor"] = i });
                Check.Equal((long)i * 2, (long)projector.ProjectToTokenWithContext(new { value = 2 }, plan, context));
            });
        });
        test("functions: explicit catalog and per-call allowlist", () =>
        {
            var p = WithFunction(new("twice", 1, 1, (a, _) => new JValue((long)a[0] * 2), "<n:n>"));
            var plan = p.Compile(new() { Expression = "$twice(n)" });
            Check.True(!p.TryProject<long>(new { n = 4 }, plan).Success);
            Check.Json("8", p.ProjectToTokenWithContext(new { n = 4 }, plan, new(allowedFunctions: new[] { "twice" })));
        });
        test("functions: zero argument callback", () =>
        {
            var p = WithFunction(new("answer", 0, 0, (_, _) => new JValue(42)));
            Check.Json("42", p.ProjectToTokenWithContext(null, p.Compile(new() { Expression = "$answer()" }), new(allowedFunctions: new[] { "answer" })));
        });
        test("functions: arguments cannot mutate input", () =>
        {
            var p = WithFunction(new("touch", 1, 1, (a, _) => { ((JObject)a[0]).Set("x", new JValue(9)); return a[0]; }));
            var source = JToken.Parse("{\"data\":{\"x\":1}}");
            var result = p.ProjectToTokenWithContext(source, p.Compile(new() { Expression = "{'changed':$touch(data),'original':data.x}" }), new(allowedFunctions: new[] { "touch" }));
            Check.Json("{\"changed\":{\"x\":9},\"original\":1}", result);
            Check.Json("{\"data\":{\"x\":1}}", source);
        });
        test("functions: host cancellation propagates", () =>
        {
            using var cancellation = new CancellationTokenSource();
            var p = WithFunction(new("stop", 0, 0, (_, token) => { cancellation.Cancel(); token.ThrowIfCancellationRequested(); return JValue.CreateNull(); }));
            Check.Throws<OperationCanceledException>(() => p.TryProject<JToken>(null, p.Compile(new() { Expression = "$stop()" }), new(allowedFunctions: new[] { "stop" }), cancellation.Token));
        });
        test("functions: return graph is bounded", () =>
        {
            var p = WithFunction(new("huge", 0, 0, (_, _) => new JValue(new string('x', 300_000))));
            Check.True(!p.TryProject<JToken>(null, p.Compile(new() { Expression = "$huge()" }), new(allowedFunctions: new[] { "huge" })).Success);
        });
        test("functions: built-ins cannot be replaced", () => Check.Throws<ProjectionConfigurationException>(() => WithFunction(new("sum", 0, 0, (_, _) => JValue.CreateNull()))));
        test("functions: unknown allowlist fails before source getter", () =>
        {
            var r = projector.TryProject<int>(new HostileSource(), Plan("Value"), new(allowedFunctions: new[] { "absent" }));
            Check.Equal(ProjectionPhase.Context, r.Diagnostics[0].Phase);
        });
        test("functions: impurity requires explicit opt-in", () =>
        {
            var p = WithFunction(new("impure", 0, 0, (_, _) => new JValue(1), IsPure: false));
            var plan = p.Compile(new() { Expression = "$impure()" });
            Check.True(!p.TryProject<int>(null, plan, new(allowedFunctions: new[] { "impure" })).Success);
            Check.Equal(1, p.TryProject<int>(null, plan, new(allowedFunctions: new[] { "impure" }, allowImpureFunctions: true)).Value);
        });
        test("functions: a variable cannot shadow an allowed function", () => Check.Throws<ProjectionConfigurationException>(() =>
            new ProjectionExecutionContext(new Dictionary<string, object?> { ["custom"] = 1 }, new[] { "custom" })));
        test("diagnostics: errors do not expose source or host messages", () =>
        {
            var p = WithFunction(new("explode", 0, 0, (_, _) => throw new InvalidOperationException("SUPER_PRIVATE_SECRET")));
            var result = p.TryProject<int>(null, p.Compile(new() { Expression = "$explode()" }), new(allowedFunctions: new[] { "explode" }));
            Check.True(!result.Success);
            Check.True(!JsonSerializer.Serialize(result.Diagnostics).Contains("SUPER_PRIVATE_SECRET", StringComparison.Ordinal));
        });
        test("diagnostics: field runtime failure", () =>
        {
            var p = projector.Compile(new() { Id = "rule", Version = 2, Fields = new() { ["Bad"] = "'private' + 1", ["Good"] = "1" } });
            var result = projector.TryProject<JToken>(null, p);
            Check.Equal("Bad", result.Diagnostics[0].Field);
            Check.Equal("T2001", result.Diagnostics[0].EngineCode);
            Check.Equal(ProjectionPhase.Evaluate, result.Diagnostics[0].Phase);
            Check.Equal(2, result.Diagnostics[0].RuleVersion);
        });
        test("diagnostics: compile error field", () =>
        {
            var result = projector.TryCompile(new() { Fields = new() { ["Bad"] = "1 +" } });
            Check.True(!result.Success);
            Check.Equal(ProjectionPhase.Compile, result.Diagnostics[0].Phase);
            Check.Equal("Bad", result.Diagnostics[0].Field);
        });
        test("diagnostics: target phase", () => Check.Equal(ProjectionPhase.Materialize,
            projector.TryProject<int>(null, Plan("'text'")).Diagnostics[0].Phase));
        test("diagnostics: cancellation is not converted to a failure result", () =>
        {
            using var c = new CancellationTokenSource(); c.Cancel();
            Check.Throws<OperationCanceledException>(() => projector.TryProject<int>(null, Plan("1"), cancellationToken: c.Token));
        });
        test("contracts: an absent provider cannot ignore a schema", () =>
        {
            using var schema = JsonDocument.Parse("false");
            Check.Throws<ProjectionConfigurationException>(() => projector.Compile(new() { Expression = "1", OutputSchema = schema.RootElement }));
        });
        test("materialize: generated metadata", () =>
        {
            var result = projector.ProjectWithTypeInfo(new { name = "Ana", total = 2.5m }, Plan("{'Name':name,'Total':total}"), TestJsonContext.Default.PersonTarget);
            Check.Equal("Ana", result!.Name); Check.Equal(2.5m, result.Total);
        });
        test("materialize: default target contract rejects extra fields", () => Check.True(!projector.TryProject<PersonTarget>(null, Plan("{'Name':'A','Total':1,'Extra':2}")).Success));
        test("materialize: required fields remain enforced", () => Check.True(!projector.TryProject<RequiredTarget>(null, Plan("{} ")).Success));
        test("materialize: enum names remain supported", () => Check.Equal(DayOfWeek.Friday, projector.Project<DayOfWeek>(null, new ProjectionDefinition { Expression = "'Friday'" })));
        test("writer: data graph roundtrip", () =>
        {
            var input = JToken.Parse("{\"a\":[1,true,null,\"quote\\\"\"],\"n\":1234567890.123456789}");
            using var d = JsonDocument.Parse(NativeJsonWriter.ToUtf8Bytes(input));
            Check.True(JToken.DeepEquals(input, Projecta.SystemTextJson.ProjectaExtensions.FromSystemTextJson(d)));
        });
        test("writer: cycles rejected", () => { var a = new JArray(); a.Add(a); Check.Throws<ObjectReadException>(() => NativeJsonWriter.ToUtf8Bytes(a)); });
        test("writer: undefined rejected", () => Check.Throws<ArgumentException>(() => NativeJsonWriter.ToUtf8Bytes(JValue.CreateUndefined())));
        test("selective: default still reads complete POCO", () => Check.Throws<ObjectReadException>(() => projector.Project<int>(new HostileSource(), Plan("Value"))));
        test("selective: static root paths avoid irrelevant getters", () =>
        {
            var p = new ObjectProjector(new() { EnableSelectiveReading = true });
            Check.Equal(7, p.Project<int>(new HostileSource(), new ProjectionDefinition { Expression = "Value" }));
        });
        test("selective: output object and arithmetic dependencies", () =>
        {
            var p = new ObjectProjector(new() { EnableSelectiveReading = true });
            Check.Json("{\"Double\":14}", p.ProjectToToken(new HostileSource(), new ProjectionDefinition { Fields = new() { ["Double"] = "Value*2" } }));
        });
        test("selective: wildcard uses full-read fallback", () =>
        {
            var p = new ObjectProjector(new() { EnableSelectiveReading = true });
            var plan = p.Compile(new() { Expression = "*" });
            Check.True(!plan.Dependencies.CanSelectRootMembers);
            Check.Throws<ObjectReadException>(() => p.ProjectToToken(new HostileSource(), plan));
        });
        test("selective: function with implicit context uses full-read fallback", () =>
            Check.True(!Plan("$keys()").Dependencies.CanSelectRootMembers));
        test("selective: full root reference uses fallback", () => Check.True(!Plan("$").Dependencies.CanSelectRootMembers));
        test("selective: collection semantics preserved", () =>
        {
            var p = new ObjectProjector(new() { EnableSelectiveReading = true });
            Check.Json("[1,2]", p.ProjectToToken(new[] { new { value = 1, other = 3 }, new { value = 2, other = 4 } }, new ProjectionDefinition { Expression = "[value]" }));
        });
        test("selective: JsonPropertyName preserved", () =>
        {
            var p = new ObjectProjector(new() { EnableSelectiveReading = true });
            Check.Equal(8, p.Project<int>(new RenamedSource(), new ProjectionDefinition { Expression = "value" }));
        });
        test("selective: equivalence across 250 generated POCO inputs", () =>
        {
            var selective = new ObjectProjector(new() { EnableSelectiveReading = true });
            foreach (var expr in new[] { "a", "a+b", "{'N':a,'D':b*2}", "a>0?a:b", "[a,b]", "nested.x" })
            {
                var definition = new ProjectionDefinition { Expression = expr };
                for (int i = 0; i < 250; i++)
                {
                    var data = new { a = i, b = 250 - i, nested = new { x = i + 1 } };
                    Check.True(JToken.DeepEquals(projector.ProjectToToken(data, definition), selective.ProjectToToken(data, definition)));
                }
            }
        });
        test("cache: bounded under concurrent churn", () =>
        {
            var p = new ObjectProjector(new() { CacheCapacity = 8, MaxConcurrentCompilations = 2 });
            Parallel.For(0, 200, i => Check.Equal(i, p.Project<int>(null, new ProjectionDefinition { Expression = i.ToString(System.Globalization.CultureInfo.InvariantCulture) })));
            Check.True(p.CachedExpressionCount <= 8); Check.True(p.CacheStatistics.Evictions > 0);
        });
        test("cache: metadata is not shared by expression identity", () =>
        {
            var a = projector.Compile(new() { Id = "a", Expression = "1" });
            var b = projector.Compile(new() { Id = "b", Expression = "1", Version = 9 });
            Check.Equal("a", a.Id); Check.Equal("b", b.Id); Check.Equal(9, b.Version);
        });
        test("plan: field dictionary mutations cannot alter a plan", () =>
        {
            var fields = new Dictionary<string, string> { ["A"] = "1" };
            var plan = projector.Compile(new() { Fields = fields }); fields["A"] = "2";
            Check.Json("{\"A\":1}", projector.ProjectToToken(null, plan));
        });
        test("plan: identical content has stable fingerprint", () => Check.Equal(Plan("a").Fingerprint, Plan("a").Fingerprint));
        test("reader: per-snapshot adapter state is isolated", () =>
        {
            var adapter = new StatefulAdapter();
            var r = new ObjectReader(adapters: new[] { adapter });
            Check.Json("[1,2]", r.Read(new[] { new Marker(), new Marker() }));
            Check.Json("[1,2]", r.Read(new[] { new Marker(), new Marker() }));
        });
    }
    private static ObjectProjector WithFunction(ProjectionFunction function) => new(new() { Functions = new(new[] { function }) });
    private sealed class HostileSource { public int Value => 7; public int Private => throw new InvalidOperationException("MUST_NOT_READ"); }
    private sealed class RenamedSource { [JsonPropertyName("value")] public int Value => 8; public int Private => throw new InvalidOperationException(); }
    private sealed class Marker;
    private sealed class StatefulAdapter : IObjectSourceAdapter
    {
        private sealed class State { public int Value; }
        public bool CanRead(object source) => source is Marker;
        public JToken Read(object source, ObjectReadContext context) => new JValue(++context.GetOrCreateState(this, () => new State()).Value);
    }
}
public sealed record PersonTarget(string Name, decimal Total);
public sealed class RequiredTarget { public required string Name { get; init; } }
[JsonSerializable(typeof(PersonTarget))]
internal partial class TestJsonContext : JsonSerializerContext;
