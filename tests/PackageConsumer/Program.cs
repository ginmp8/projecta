using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Projecta.Contracts;
using Projecta.Documents;
using Projecta.Generated;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.Queryable;
using Projecta.Streaming;

int checks = 0;
void Verify(bool success) { if (!success) throw new InvalidOperationException("Package consumer assertion failed: " + checks); checks++; }
var catalog = new ProjectionFunctionCatalog(new[] { new ProjectionFunction("cleanName", 1, 1,
    (a, token) => { token.ThrowIfCancellationRequested(); return new JValue(((string)a[0]).Trim()); }) });
var projector = new ObjectProjector(new ProjectionOptions { ContractProvider = new JsonSchemaContractProvider(), Functions = catalog });
var definition = ProjectionDefinition.Parse("""
{"id":"package-consumer","inputSchema":{"type":"object","required":["id","name"],"properties":{"id":{"type":"integer"},"name":{"type":"string"}}},
"outputSchema":{"type":"object","required":["Id","Name"],"properties":{"Id":{"type":"integer"},"Name":{"type":"string"}},"additionalProperties":false},
"fields":{"Id":"id * $factor","Name":"$cleanName(name)"}}
""");
var context = new ProjectionExecutionContext(new Dictionary<string, object?> { ["factor"] = 2 }, new[] { "cleanName" });
var plan = projector.Compile(definition);
var result = projector.TryProject<View>(new { id = 21, name = " Ana " }, plan, context);
Verify(result.Success && result.Value == new View(42, "Ana"));
var failed = projector.TryProject<View>(new { id = "private", name = "Ana" }, plan, context);
Verify(!failed.Success && failed.Diagnostics[0].Phase == ProjectionPhase.InputContract);
Verify(!JsonSerializer.Serialize(failed.Diagnostics).Contains("private", StringComparison.Ordinal));
Verify(projector.ProjectWithTypeInfo(new { id = 21, name = " Ana " }, plan, Models.Default.View, context) == new View(42, "Ana"));
var input = JToken.Parse("{\"id\":1,\"name\":\"old\"}");
var patched = JsonPatchDocument.Parse("[{\"op\":\"replace\",\"path\":\"/name\",\"value\":\"new\"}]").Apply(input);
Verify((string)JsonPointer.Parse("/name").Resolve(patched) == "new");
Verify((string)JsonPointer.Parse("/name").Resolve(input) == "old");
Verify(JsonMergePatch.Apply(input, JToken.Parse("{\"name\":null}")) is JObject merged && !merged.Properties.ContainsKey("name"));
var pipeline = DocumentPipeline.Parse("{\"version\":1,\"steps\":[{\"op\":\"default\",\"path\":\"/tags\",\"value\":\"reader\"},{\"op\":\"ensureArray\",\"path\":\"/tags\"}]}");
Verify(JsonPointer.Parse("/tags").Resolve(pipeline.Execute(input).Value) is JArray);
var jsonl = new MemoryStream(Encoding.UTF8.GetBytes("{\"id\":1,\"name\":\"a\"}\n{\"id\":2,\"name\":\"b\"}\n"));
var rows = new List<ProjectionItemResult<View>>();
await foreach (var row in projector.ProjectJsonLinesAsync<View>(jsonl, plan, context)) rows.Add(row);
Verify(rows.Count == 2 && rows.All(x => x.Result.Success) && rows[1].Result.Value == new View(4, "b"));
Verify(new[] { new Source { Id = 2, Name = "query" } }.AsQueryable().ProjectTo<Source, View>(
    new ProjectionDefinition { Expression = "{'Id':Id,'Name':Name}" }).Single() == new View(2, "query"));
Verify(Mappers.Map(new Source { Id = 42, Name = "generated" }) == new View(42, "generated"));
Verify(projector.CachedExpressionCount > 0);
var inspector = new ProjectionInspector(projector);
var inspected = inspector.Inspect(definition, typeof(Source), typeof(View));
Verify(inspected.Explanation.RuleId == definition.Id && inspected.Explanation.RuleVersion == definition.Version);
Verify(inspector.Explain(plan).HasInputContract && inspector.Explain(plan).HasOutputContract);
var registry = new ProjectionRuleRegistry(projector);
var staged = registry.Stage(definition);
Verify(registry.GetActive(definition.Id) is null);
Verify(registry.TryActivate(definition.Id, definition.Version, expectedActiveVersion: null));
Verify(registry.GetActive(definition.Id)?.Fingerprint == staged.Fingerprint);
Verify(!registry.TryActivate(definition.Id, definition.Version, expectedActiveVersion: null));
var difference = new ProjectionComparer(projector).Compare(new { id = 21, name = " Ana " }, plan, staged, context);
Verify(difference.Equivalent && difference.BeforeRuleId == definition.Id && difference.AfterRuleId == definition.Id);
Verify(difference.BeforeVersion == definition.Version && difference.AfterVersion == definition.Version);
var replay = ProjectionReplayIdentity.Create(plan, projector.Options, context);
Verify(!replay.CanReproduce && replay.Limitations.Contains("source-values-unpinned"));
var omitProjector = new ObjectProjector(new ProjectionOptions { NullProperties = ProjectionNullProperties.Omit });
var omitPlan = omitProjector.Compile(new ProjectionDefinition { Expression = "{'present':1,'absent':null,'array':[null]}" });
Verify(JToken.DeepEquals(omitProjector.ProjectToToken(new { }, omitPlan), JToken.Parse("{\"present\":1,\"array\":[null]}")));
Console.WriteLine($"PACKAGE-CONSUMER passed={checks} failed=0 source=NuGet");

public sealed record View(long Id, string Name);
public sealed class Source { public int Id { get; init; } public string Name { get; init; } = ""; }
public static partial class Mappers
{
    [StaticProjection("{'Id':Id,'Name':Name}")]
    public static partial View Map(Source source);
}
[JsonSerializable(typeof(View))]
internal partial class Models : JsonSerializerContext { }
