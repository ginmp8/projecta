using Projecta;
using Projecta.Json;
using Projecta.Mapping;
using System.Text.Json;

var tests = new List<(string Name, Func<Task> Run)>();
void Test(string name, Action run) => tests.Add((name, () => { run(); return Task.CompletedTask; }));
Test("contract: execution context is available", () => Check.True(typeof(ObjectProjector).Assembly.GetType("Projecta.Mapping.ProjectionExecutionContext") is not null));
Test("contract: structured diagnostic is available", () => Check.True(typeof(ObjectProjector).Assembly.GetType("Projecta.Mapping.ProjectionDiagnostic") is not null));
Test("contract: schema provider boundary is available", () => Check.True(typeof(ObjectProjector).Assembly.GetType("Projecta.Mapping.IProjectionContractProvider") is not null));
Test("contract: dependency analysis is available", () => Check.True(typeof(ProjectaQuery).Assembly.GetType("Projecta.Analysis.QueryDependencyAnalysis") is not null));
Test("diagnostic: arithmetic errors retain AST position", () =>
{
    var ex = Check.Throws<JsonataException>(() => new ProjectaQuery("{'Total':('private-value' + 1)}").Eval(JToken.Parse("{}")));
    Check.True(ex.Position is >= 0);
});
CoreEvolutionTests.Register(Test);
SchemaTests.Register(Test);
AdversarialTests.Register(Test);
ToolingTests.Register(Test);
GeneratorTests.Register(Test);
QueryableTests.Register(Test);
DocumentTests.Register(Test);
StreamingTests.Register(Test);
NumericTests.Register(Test);
var results = new List<object>();
int failed = 0;
foreach (var t in tests)
{
    try { await t.Run(); results.Add(new { name = t.Name, passed = true }); Console.WriteLine("PASS " + t.Name); }
    catch (Exception ex) { failed++; results.Add(new { name = t.Name, passed = false, error = ex.ToString() }); Console.WriteLine("FAIL " + t.Name + ": " + ex.Message); }
}
Console.WriteLine($"RESULT passed={tests.Count - failed} failed={failed} total={tests.Count}");
if (args is ["--out", var path]) File.WriteAllText(path, JsonSerializer.Serialize(new { passed = tests.Count - failed, failed, total = tests.Count, results }, new JsonSerializerOptions { WriteIndented = true }));
return failed == 0 && tests.Count >= 5 ? 0 : 1;

internal static class Check
{
    internal static void True(bool value) { if (!value) throw new Exception("Assertion failed."); }
    internal static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, actual {actual}."); }
    internal static T Throws<T>(Action action) where T : Exception
    { try { action(); } catch (T e) { return e; } throw new Exception("Expected " + typeof(T).Name); }
    internal static async Task<T> ThrowsAsync<T>(Func<Task> action) where T : Exception
    { try { await action(); } catch (T e) { return e; } throw new Exception("Expected " + typeof(T).Name); }
    internal static void Json(string expected, JToken actual) { True(JToken.DeepEquals(JToken.Parse(expected), actual)); }
}
