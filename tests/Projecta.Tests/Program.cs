using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Projecta;
using Projecta.Execution;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.Objects;
using Projecta.SystemTextJson;
using NativeObject = Projecta.Json.JObject;
using NativeArray = Projecta.Json.JArray;

var results = new List<object>(); int passed = 0, failed = 0;
void Test(string name, Action test)
{
    try { test(); ++passed; results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { ++failed; results.Add(new { name, passed = false, error = ex.ToString() }); Console.WriteLine("FAIL " + name + ": " + ex); }
}
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
void Throws<T>(Action action) where T : Exception
{ try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
void ContainsException<T>(Action action) where T : Exception
{
    try { action(); }
    catch (Exception ex) { for (Exception? e = ex; e is not null; e = e.InnerException) if (e is T) return; throw; }
    throw new Exception("Expected nested " + typeof(T).Name);
}
var projector = new ObjectProjector();
ProjectionDefinition Expr(string expression) => new() { Expression = expression };
JToken Eval(string expression, string data = "null") => new ProjectaQuery(expression).Eval(JToken.Parse(data));
void JsonEqual(string expected, JToken actual) { if (!JToken.DeepEquals(JToken.Parse(expected), actual)) throw new Exception("Unexpected JSON: " + actual.ToFlatString()); }

Test("reader: ignores indexers", () => Equal(1, ((NativeObject)JToken.FromObject(new Indexed())).Count));
Test("reader: ignores static properties", () => Equal(1, ((NativeObject)JToken.FromObject(new StaticProperties())).Count));
Test("reader: lazy enumerable", () => JsonEqual("[1,2]", JToken.FromObject(Values())));
Test("reader: enum string", () => Equal("Monday", (string)JToken.FromObject(DayOfWeek.Monday)));
Test("reader: guid", () => { var id = Guid.NewGuid(); Equal(id.ToString("D"), (string)JToken.FromObject(id)); });
Test("reader: null", () => Equal(JTokenType.Null, JToken.FromObject(null).Type));
Test("reader: bool", () => Equal(true, (bool)JToken.FromObject(true)));
Test("reader: ulong", () => Equal((decimal)ulong.MaxValue, (decimal)JToken.FromObject(ulong.MaxValue)));
Test("reader: date only", () => Equal("2026-10-09", (string)JToken.FromObject(new DateOnly(2026, 10, 9))));
Test("reader: date offset", () => { var d = DateTimeOffset.Parse("2026-10-09T10:00:00-03:00"); Equal(d.ToString("O"), (string)JToken.FromObject(d)); });
Test("reader: decimal precision", () => Equal(1234567890.123456789m, (decimal)JToken.FromObject(1234567890.123456789m)));
Test("reader: nonfinite float", () => Throws<ObjectReadException>(() => JToken.FromObject(double.NaN)));
Test("reader: dictionary", () => JsonEqual("{\"value\":42}", JToken.FromObject(new Dictionary<string, object> { ["value"] = 42 })));
Test("reader: rejects nonstring dictionary keys", () => Throws<ObjectReadException>(() => JToken.FromObject(new Dictionary<int, string> { [1] = "v" })));
Test("reader: expando", () => { IDictionary<string, object?> e = new ExpandoObject(); e["Name"] = "Ana"; JsonEqual("{\"Name\":\"Ana\"}", JToken.FromObject(e)); });
Test("reader: circular CLR", () => { var c = new Cyclic(); c.Next = c; Throws<ObjectReadException>(() => JToken.FromObject(c)); });
Test("reader: circular native JSON", () => { var a = new NativeArray(); a.Add(a); Throws<ObjectReadException>(() => JToken.FromObject(a)); });
Test("reader: shared reference is not a cycle", () => { var item = new { Id = 1 }; JsonEqual("[{\"Id\":1},{\"Id\":1}]", JToken.FromObject(new[] { item, item })); });
Test("reader: unbounded enumeration stops", () => Throws<ObjectReadException>(() => new ObjectReader(new() { MaxNodes = 5 }).Read(Forever())));
Test("reader: depth limit", () => Throws<ObjectReadException>(() => new ObjectReader(new() { MaxDepth = 2 }).Read(new { A = new { B = 3 } })));
Test("reader: character limit", () => Throws<ObjectReadException>(() => new ObjectReader(new() { MaxCharacters = 3 }).Read("long")));
Test("reader: getter exception is attributed", () => Throws<ObjectReadException>(() => JToken.FromObject(new ThrowingGetter())));
Test("reader: allowlist avoids getter", () => JsonEqual("{}", new ObjectReader(new() { IncludeProperty = (_, _) => false }).Read(new ThrowingGetter())));
Test("reader: JSON attributes", () => JsonEqual("{\"display\":\"Ana\"}", JToken.FromObject(new Attributed())));
Test("reader: IQueryable rejected before enumeration", () => Throws<ObjectReadException>(() => JToken.FromObject(new[] { 1, 2 }.AsQueryable())));
Test("reader: JsonElement", () => { using var d = JsonDocument.Parse("{\"a\":[1,true,null]}"); JsonEqual("{\"a\":[1,true,null]}", JToken.FromObject(d.RootElement)); });
Test("reader: JsonNode", () => JsonEqual("{\"a\":2}", JToken.FromObject(JsonNode.Parse("{\"a\":2}"))));
Test("reader: clones native input", () => { var n = (NativeObject)JToken.Parse("{\"a\":1}"); var copy = (NativeObject)JToken.FromObject(n); copy.Set("a", new JValue(2)); Equal(1, (int)n.Properties["a"]); });
Test("reader: nested custom adapter", () => { var r = new ObjectReader(adapters: new[] { new SafeSourceAdapter() }); JsonEqual("[{\"Name\":\"safe\"}]", r.Read(new[] { new ThrowingGetter() })); });
Test("reader: cancelled before getters", () => { using var c = new CancellationTokenSource(); c.Cancel(); Throws<OperationCanceledException>(() => new ObjectReader().Read(new ThrowingGetter(), c.Token)); });
Test("reader: cancellation during enumeration", () => { using var c = new CancellationTokenSource(); Throws<OperationCanceledException>(() => new ObjectReader().Read(Cancelling(c), c.Token)); });

Test("projection: DTO to typed DTO", () =>
{
    var rule = ProjectionDefinition.Parse("""{"id":"customer","fields":{"Id":"$string(id)","Name":"fullName","IsActive":"status = 'ACTIVE'"}}""");
    var result = projector.Project<CustomerView>(new { id = 123, fullName = "Ana", status = "ACTIVE" }, rule)!;
    Equal("123", result.Id); Equal("Ana", result.Name); Equal(true, result.IsActive);
});
Test("projection: constructor record target", () => Equal("Ana", projector.Project<Named>(new { name = "Ana" }, Expr("{'Name':name}"))!.Name));
Test("projection: nested lists and nulls", () => JsonEqual("{\"a\":[1,2],\"b\":null}", projector.ProjectToToken(new { values = new[] { 1, 2 } }, Expr("{'a':values,'b':null}"))));
Test("projection: array typed target", () => Equal(3, projector.Project<int[]>(new[] { 1, 2, 3 }, Expr("$"))!.Length));
Test("projection: nullable scalar target", () => Equal<int?>(null, projector.Project<int?>(null, Expr("null"))));
Test("projection: invalid scalar null target", () => Throws<ProjectionMaterializationException>(() => projector.Project<int>(null, Expr("null"))));
Test("projection: JSON node output", () => Equal(5L, projector.Project<JsonObject>(new { a = 5 }, Expr("$"))!["a"]!.GetValue<long>()));
Test("projection: JsonDocument output", () => { using var d = projector.Project<JsonDocument>(new { a = 5 }, Expr("$")); Equal(5, d!.RootElement.GetProperty("a").GetInt32()); });
Test("projection: JsonElement output detached", () => Equal(5, projector.Project<JsonElement>(new { a = 5 }, Expr("$" )).GetProperty("a").GetInt32()));
Test("projection: enum target", () => Equal(DayOfWeek.Monday, projector.Project<DayOfWeek>("Monday", Expr("$"))));
Test("projection: decimal target", () => Equal(123.456789m, projector.Project<decimal>(123.456789m, Expr("$"))));
Test("projection: registered target alias", () => { var r = new ProjectionTypeRegistry().Register<Named>("named"); var rule = new ProjectionDefinition { TargetType = "named", Expression = "{'Name':name}" }; Equal("Ana", ((Named)projector.Project(new { name = "Ana" }, rule, r)!).Name); });
Test("projection: unregistered target rejected", () => Throws<ProjectionConfigurationException>(() => new ProjectionTypeRegistry().Resolve("System.IO.File")));
Test("projection: duplicate alias rejected", () => Throws<ProjectionConfigurationException>(() => new ProjectionTypeRegistry().Register<int>("a").Register<string>("a")));
Test("projection: undefined is explicit error", () => Throws<ProjectionMaterializationException>(() => projector.ProjectToToken(new { a = 1 }, Expr("missing"))));
Test("projection: unknown target member rejected", () => Throws<ProjectionMaterializationException>(() => projector.Project<Named>(new { bad = 1 }, Expr("$"))));
Test("projection: required target property", () => Throws<ProjectionMaterializationException>(() => projector.Project<RequiredTarget>(null, Expr("{}"))));
Test("projection: expression/fields conflict", () => Throws<ProjectionConfigurationException>(() => projector.Compile(new() { Expression = "$", Fields = new() { ["A"] = "1" } })));
Test("projection: empty rule rejected", () => Throws<ProjectionConfigurationException>(() => projector.Compile(new())));
Test("projection: duplicate config property rejected", () => Throws<ProjectionConfigurationException>(() => ProjectionDefinition.Parse("{\"expression\":\"1\",\"Expression\":\"2\"}")));
Test("projection: unknown config property rejected", () => Throws<JsonException>(() => ProjectionDefinition.Parse("{\"expression\":\"1\",\"typo\":1}")));
Test("projection: no type metadata instantiation", () => Throws<ProjectionMaterializationException>(() => projector.Project<Named>(null, Expr("{'$type':'System.IO.File','Name':'Ana'}"))));
Test("projection: cache bounded", () => { var p = new ObjectProjector(new() { CacheCapacity = 2 }); for (int i = 0; i < 8; ++i) p.Compile(Expr(i.ToString())); Equal(2, p.CachedExpressionCount); });
Test("projection: compiled rule snapshot", () => { var rule = new ProjectionDefinition { Fields = new() { ["Name"] = "'first'" } }; var plan = projector.Compile(rule); rule.Fields["Name"] = "'second'"; Equal("first", projector.Project<Named>(null, plan)!.Name); });
Test("projection: parallel shared compiled plan", () => { var plan = projector.Compile(Expr("a*2")); Parallel.For(0, 100, i => Equal(i * 2, projector.Project<int>(new { a = i }, plan))); });
Test("projection: output character limit", () => Throws<ObjectReadException>(() => new ObjectProjector(new() { Output = new() { MaxCharacters = 3 } }).ProjectToToken("long", Expr("$"))));

Test("engine: steps enforced", () => Throws<ExecutionLimitException>(() => new ProjectaQuery("$sum([1..100])").Eval(JValue.CreateNull(), new EvaluationLimits { MaxSteps = 10 })));
Test("engine: range cap", () => Throws<ExecutionLimitException>(() => new ProjectaQuery("[1..6]").Eval(JValue.CreateNull(), new EvaluationLimits { MaxRangeLength = 5 })));
Test("engine: range upper long boundary", () => { var data = JToken.FromObject(new { a = long.MaxValue - 1, b = long.MaxValue }); Equal(2, ((NativeArray)new ProjectaQuery("[a..b]").Eval(data)).Count); });
Test("engine: allocation cap", () => Throws<ExecutionLimitException>(() => new ProjectaQuery("[1..100]").Eval(JValue.CreateNull(), new EvaluationLimits { MaxAllocatedTokens = 5 })));
Test("engine: cancellation", () => { using var c = new CancellationTokenSource(); c.Cancel(); Throws<OperationCanceledException>(() => new ProjectaQuery("1+2").Eval(JValue.CreateNull(), new EvaluationLimits(), c.Token)); });
Test("engine: timeout", () => Throws<ExecutionLimitException>(() => new ProjectaQuery("1+2").Eval(JValue.CreateNull(), new EvaluationLimits { Timeout = TimeSpan.FromTicks(1) })));
Test("engine: minimum integer padding width rejected", () => Throws<ExecutionLimitException>(() => Eval("$pad('a',-2147483648,'x')")));
Test("engine: string limit", () => Throws<ExecutionLimitException>(() => new ProjectaQuery("'long'&'string'").Eval(JValue.CreateNull(), new EvaluationLimits { MaxStringLength = 3 })));
Test("engine: dynamic evaluation default denied", () => Throws<ExecutionLimitException>(() => Eval("$eval('1+1')")));
Test("engine: dynamic evaluation explicit and local scope", () => Equal(7, (int)new ProjectaQuery("($x := 7; $eval('$x'))").Eval(JValue.CreateNull(), new EvaluationLimits { AllowDynamicEvaluation = true })));
Test("engine: nested dynamic evaluation inherits budget", () => Throws<ExecutionLimitException>(() => new ProjectaQuery("$eval('[1..100]')").Eval(JValue.CreateNull(), new EvaluationLimits { AllowDynamicEvaluation = true, MaxRangeLength = 5 })));
Test("engine: default environments isolated", () => { var env = EvaluationEnvironment.DefaultEnvironment; env.BindValue("leak", new JValue(1)); Equal(JTokenType.Undefined, Eval("$leak").Type); });
Test("engine: failed call cannot poison next budget", () => { Throws<ExecutionLimitException>(() => new ProjectaQuery("[1..5]").Eval(JValue.CreateNull(), new EvaluationLimits { MaxSteps = 1 })); Equal(3, (int)Eval("1+2")); });
Test("engine: standalone regex parses", () => _ = new ProjectaQuery("/abc/"));
Test("engine: regex timeout", () => ContainsException<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => new ProjectaQuery("$contains(value,/^(a+)+$/)").Eval(JToken.FromObject(new { value = new string('a', 100) + "!" }))));
Test("engine: parser depth bounded", () => Throws<ExecutionLimitException>(() => new ProjectaQuery(new string('(', 200) + "1" + new string(')', 200))));
Test("engine: optimizer depth bounded", () => Throws<ExecutionLimitException>(() => new ProjectaQuery(string.Join("+", Enumerable.Repeat("1", 300)))));
Test("engine: expression length bounded", () => Throws<ExecutionLimitException>(() => new ProjectaQuery(new string(' ', 40_000) + "1")));
Test("JSON parser: depth", () => Throws<Projecta.Json.JsonParseException>(() => JToken.Parse(new string('[', 80) + "0" + new string(']', 80))));
Test("JSON parser: input characters", () => Throws<Projecta.Json.JsonParseException>(() => JToken.Parse("[1,2,3]", new ParseSettings { MaxCharacters = 4 })));
Test("JSON parser: token count", () => Throws<Projecta.Json.JsonParseException>(() => JToken.Parse("[1,2,3]", new ParseSettings { MaxTokens = 2 })));
Test("JSON parser: string length", () => Throws<Projecta.Json.JsonParseException>(() => JToken.Parse("\"long\"", new ParseSettings { MaxStringLength = 2 })));
Test("JSON parser: strict undefined", () => Throws<Projecta.Json.JsonParseException>(() => JToken.Parse("undefined", ParseSettings.GetStrict())));
Test("JSON parser: strict trailing comma", () => Throws<Projecta.Json.JsonParseException>(() => JToken.Parse("[1,]", ParseSettings.GetStrict())));
Test("JSON parser: async depth", () => Throws<Projecta.Json.JsonParseException>(() => JToken.ParseAsync(new StringReader(new string('[', 80) + "0" + new string(']', 80)), default).GetAwaiter().GetResult()));
Test("JSON parser: async validation depth", () => Throws<Projecta.Json.JsonParseException>(() => JToken.ValidateAsync(new StringReader(new string('[', 80) + "0" + new string(']', 80)), default).GetAwaiter().GetResult()));
Test("JSON parser: async normal", () => JsonEqual("{\"a\":[1,true,null]}", JToken.ParseAsync(new StringReader("{\"a\":[1,true,null]}"), default).GetAwaiter().GetResult()));

var language = new (string Name, string Expr, string Input, string Expected)[]
{
    ("sum", "$sum(values)", "{\"values\":[1,2,3]}", "6"),
    ("filter", "values[$ > 1]", "{\"values\":[1,2,3]}", "[2,3]"),
    ("map", "$map(values,function($v){$v*2})", "{\"values\":[1,2,3]}", "[2,4,6]"),
    ("reduce", "$reduce(values,function($a,$v){$a+$v},0)", "{\"values\":[1,2,3]}", "6"),
    ("condition", "a>1?'yes':'no'", "{\"a\":2}", "\"yes\""),
    ("count empty", "$count([])", "null", "0"),
    ("nested object", "{'n':data.name}", "{\"data\":{\"name\":\"Ana\"}}", "{\"n\":\"Ana\"}"),
    ("uppercase", "$uppercase('abc')", "null", "\"ABC\""),
    ("substring", "$substring('abcdef',1,3)", "null", "\"bcd\""),
    ("sort", "$sort([3,1,2])", "null", "[1,2,3]"),
    ("distinct", "$distinct([1,2,1])", "null", "[1,2]"),
    ("merge", "$merge([{'a':1},{'b':2}])", "null", "{\"a\":1,\"b\":2}"),
    ("append", "$append([1],[2,3])", "null", "[1,2,3]"),
    ("lookup", "$lookup({'a':3},'a')", "null", "3"),
    ("existence fallback", "$exists(missing)?missing:0", "{}", "0"),
    ("regex contains", "$contains('abc',/b/)", "null", "true"),
    ("regex match", "$match('a1b2',/[0-9]/).match", "null", "[\"1\",\"2\"]"),
    ("string replace", "$replace('one-two','-',' ')", "null", "\"one two\""),
    ("string padding", "$pad('a',4,'xy')", "null", "\"axyx\""),
    ("left padding", "$pad('a',-4,'xy')", "null", "\"yxya\""),
    ("number conversion", "$number('12')", "null", "12"),
    ("singleton array", "[values.value]", "{\"values\":[{\"value\":1}]}", "[1]"),
    ("empty array", "[values.value]", "{\"values\":[]}", "[]"),
    ("average", "$average([2,4,6])", "null", "4"),
    ("boolean", "$boolean([0,1])", "null", "true")
};
foreach (var c in language) Test("language: " + c.Name, () => JsonEqual(c.Expected, Eval(c.Expr, c.Input)));
Test("JSON differential corpus: 200 objects", () =>
{
    var random = new Random(7521);
    for (int i = 0; i < 200; ++i)
    {
        var data = new { id = i, value = random.Next(-10000, 10000), text = "quote\" slash\\ tab\t unicode \u00e7", items = new[] { i, i + 1 } };
        string json = JsonSerializer.Serialize(data);
        var n = JToken.Parse(json, ParseSettings.GetStrict());
        using var parsed = JsonDocument.Parse(n.ToFlatString());
        Equal(i, parsed.RootElement.GetProperty("id").GetInt32());
        Equal(data.text, parsed.RootElement.GetProperty("text").GetString());
    }
});
Console.WriteLine($"RESULT passed={passed} failed={failed} total={passed + failed}");
if (args.Length == 2 && args[0] == "--out") File.WriteAllText(args[1], JsonSerializer.Serialize(new { passed, failed, total = passed + failed, results }, new JsonSerializerOptions { WriteIndented = true }));
return failed == 0 && passed >= 90 ? 0 : 1;

IEnumerable<int> Values() { yield return 1; yield return 2; }
IEnumerable<int> Forever() { while (true) yield return 1; }
IEnumerable<int> Cancelling(CancellationTokenSource c) { yield return 1; c.Cancel(); yield return 2; }
public class Indexed { public string Name => "ok"; public int this[int index] => index; }
public class StaticProperties { public string Name => "ok"; public static string Static => "ignored"; }
public class Cyclic { public Cyclic? Next { get; set; } }
public class ThrowingGetter { public string Name => throw new InvalidOperationException("Getter ran"); }
public class Attributed { [JsonPropertyName("display")] public string Name => "Ana"; [JsonIgnore] public string Secret => throw new Exception("Ignored getter"); }
public record Named(string Name);
public class RequiredTarget { public required string Name { get; init; } }
public class CustomerView { public string? Id { get; set; } public string? Name { get; set; } public bool IsActive { get; set; } }
public class SafeSourceAdapter : IObjectSourceAdapter
{
    public bool CanRead(object source) => source is ThrowingGetter;
    public JToken Read(object source, ObjectReadContext context) { var result = new NativeObject(); result.Add("Name", context.Read("safe")); return result; }
}
