using System.Text.Json;
using Projecta.Tooling;
string fixture=Path.Combine(AppContext.BaseDirectory,"fixtures","semantics.json");
using var suite=JsonDocument.Parse(File.ReadAllBytes(fixture));
var result=SemanticSuite.Run(suite.RootElement);
Console.WriteLine($"SEMANTICS total={result.Total} passed={result.Passed} failed={result.Failed} skipped={result.Skipped}");
foreach(var failure in result.Cases.Where(x=>!x.Passed)) Console.WriteLine($"FAIL {failure.Id}: {failure.Outcome} {failure.ErrorCode}");
if(args is ["--out",var path]) File.WriteAllText(path,JsonSerializer.Serialize(result,new JsonSerializerOptions {WriteIndented=true}));
return result.Total>0 && result.Failed==0 ? 0 : 1;
