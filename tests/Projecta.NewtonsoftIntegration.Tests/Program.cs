using System;
using System.Diagnostics;
using System.Globalization;
using Newtonsoft.Json.Linq;
using Projecta.Mapping;
using Projecta.NewtonsoftJson;
using Projecta.Objects;

int passed = 0, failed = 0;
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Test(string name, Action action) { try { action(); passed++; Console.WriteLine("PASS " + name); } catch(Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); } }
void Throws<T>(Action action) where T:Exception { try { action(); } catch(T) { return; } throw new Exception("Expected " + typeof(T).Name); }
var p = new ObjectProjector(sourceAdapters: new[] { new NewtonsoftSourceAdapter() }, targetMaterializers: new[] { new NewtonsoftTargetMaterializer() });
ProjectionDefinition Rule(string expression) => new() { Expression = expression };
Test("JObject to DTO", () => Check(p.Project<Person>(JObject.Parse("{\"name\":\"Ana\"}"), Rule("{'Name':name}"))!.Name == "Ana"));
Test("DTO to JObject", () => Check(p.Project<JObject>(new { Name = "Ana" }, Rule("$"))!.Value<string>("Name") == "Ana"));
Test("JArray to typed array", () => Check(p.Project<int[]>(JArray.Parse("[1,2]"), Rule("$"))!.Length == 2));
Test("JArray target", () => Check(p.Project<JArray>(new[] { 1, 2 }, Rule("$"))!.Count == 2));
Test("JValue null", () => Check(p.Project<JToken>(JValue.CreateNull(), Rule("$"))!.Type == JTokenType.Null));
Test("Decimal precision", () => Check(p.Project<decimal>(new JValue(123.123456789m), Rule("$")) == 123.123456789m));
Test("DateTimeOffset preserves offset", () => { var d = DateTimeOffset.Parse("2026-10-09T10:00:00-03:00"); Check(p.Project<string>(new JValue(d), Rule("$")) == d.ToString("O", CultureInfo.InvariantCulture)); });
Test("Guid conversion", () => { var id = Guid.NewGuid(); Check(p.Project<Guid>(new JValue(id), Rule("$")) == id); });
Test("Raw rejected", () => Throws<ObjectReadException>(() => p.ProjectToToken(new JRaw("{evil}"), Rule("$"))));
Test("Wrong token shape rejected", () => Throws<ProjectionMaterializationException>(() => p.Project<JObject>(new[] { 1, 2 }, Rule("$"))));
Test("Input is not mutated", () => { var input = JObject.Parse("{\"n\":1}"); var output = p.Project<JObject>(input, Rule("{'n':n+1}"))!; Check(input.Value<int>("n") == 1 && output.Value<int>("n") == 2); });
Test("Nested JObjects", () => Check(p.Project<int>(new { data = JObject.Parse("{\"a\":3}") }, Rule("data.a")) == 3));
Console.WriteLine("Newtonsoft assembly: " + typeof(JObject).Assembly.GetName().Version);
Console.WriteLine("Newtonsoft file: " + FileVersionInfo.GetVersionInfo(typeof(JObject).Assembly.Location).FileVersion);
Console.WriteLine($"RESULT passed={passed} failed={failed}"); return failed == 0 && passed == 12 ? 0 : 1;
public record Person(string Name);
