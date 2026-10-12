using Projecta.Tooling;
using System.Text.Json;
internal static class ToolingTests
{
    internal static void Register(Action<string, Action> test)
    {
        void WithFiles(Action<string> action)
        {
            string path=Path.Combine(Path.GetTempPath(),"projecta-test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path);
            try {File.WriteAllText(Path.Combine(path,"rule.json"),"{\"fields\":{\"n\":\"a+1\"}}"); File.WriteAllText(Path.Combine(path,"input.json"),"{\"a\":2}"); action(path);}
            finally {Directory.Delete(path,true);}
        }
        test("cli: validates rule",()=>WithFiles(p=>
        { using var output=new StringWriter(); using var error=new StringWriter(); int code=ProjectionCli.RunAsync(new[]{"validate","--rule",Path.Combine(p,"rule.json")},output,error).GetAwaiter().GetResult(); Check.Equal(0,code); using var j=JsonDocument.Parse(output.ToString()); Check.True(j.RootElement.GetProperty("valid").GetBoolean()); }));
        test("cli: previews projection",()=>WithFiles(p=>
        { using var output=new StringWriter(); using var error=new StringWriter(); Check.Equal(0,ProjectionCli.RunAsync(new[]{"preview","--rule",Path.Combine(p,"rule.json"),"--input",Path.Combine(p,"input.json")},output,error).GetAwaiter().GetResult()); Check.Json("{\"n\":3}",Projecta.Json.JToken.Parse(output.ToString())); }));
        test("cli: explains plan without input",()=>WithFiles(p=>
        { using var output=new StringWriter(); using var error=new StringWriter();
            Check.Equal(0,ProjectionCli.RunAsync(new[]{"explain","--rule",Path.Combine(p,"rule.json")},output,error).GetAwaiter().GetResult());
            using var j=JsonDocument.Parse(output.ToString());
            Check.Equal("projection",j.RootElement.GetProperty("RuleId").GetString());
            Check.True(j.RootElement.GetProperty("Paths").EnumerateArray().Any(x=>x.GetString()=="/a"));
        }));
        test("cli: compares rule versions and preserves both identities",()=>WithFiles(p=>
        {
            File.WriteAllText(Path.Combine(p,"before.json"),"{\"id\":\"old\",\"version\":4,\"fields\":{\"n\":\"a+1\"}}");
            File.WriteAllText(Path.Combine(p,"after.json"),"{\"id\":\"new\",\"version\":5,\"fields\":{\"n\":\"a+2\"}}");
            using var o=new StringWriter(); using var e=new StringWriter();
            Check.Equal(0,ProjectionCli.RunAsync(new[]{"compare","--before",Path.Combine(p,"before.json"),"--after",Path.Combine(p,"after.json"),"--input",Path.Combine(p,"input.json")},o,e).GetAwaiter().GetResult());
            using var j=JsonDocument.Parse(o.ToString());
            Check.Equal("old",j.RootElement.GetProperty("BeforeRuleId").GetString());
            Check.Equal(4,j.RootElement.GetProperty("BeforeVersion").GetInt32());
            Check.Equal("new",j.RootElement.GetProperty("AfterRuleId").GetString());
            Check.Equal(5,j.RootElement.GetProperty("AfterVersion").GetInt32());
            Check.True(j.RootElement.GetProperty("Differences").GetArrayLength()>0);
        }));
        test("cli: compare report never includes private input values",()=>WithFiles(p=>
        {
            File.WriteAllText(Path.Combine(p,"input.json"),"{\"a\":\"private-123\"}");
            File.WriteAllText(Path.Combine(p,"before.json"),"{\"id\":\"before\",\"expression\":\"a\"}");
            File.WriteAllText(Path.Combine(p,"after.json"),"{\"id\":\"after\",\"expression\":\"'updated'\"}");
            using var o=new StringWriter(); using var e=new StringWriter();
            Check.Equal(0,ProjectionCli.RunAsync(new[]{"compare","--before",Path.Combine(p,"before.json"),"--after",Path.Combine(p,"after.json"),"--input",Path.Combine(p,"input.json")},o,e).GetAwaiter().GetResult());
            Check.True(!o.ToString().Contains("private-123") && !e.ToString().Contains("private-123"));
        }));
        test("cli: dynamic output keys cannot leak source values through difference pointers",()=>WithFiles(p=>
        {
            File.WriteAllText(Path.Combine(p,"input.json"),"{\"a\":\"private-123\"}");
            File.WriteAllText(Path.Combine(p,"before.json"),"{\"expression\":\"{a:1}\"}");
            File.WriteAllText(Path.Combine(p,"after.json"),"{\"expression\":\"{}\"}");
            using var o=new StringWriter(); using var e=new StringWriter();
            Check.Equal(0,ProjectionCli.RunAsync(new[]{"compare","--before",Path.Combine(p,"before.json"),"--after",Path.Combine(p,"after.json"),"--input",Path.Combine(p,"input.json")},o,e).GetAwaiter().GetResult());
            Check.True(!o.ToString().Contains("private-123") && !e.ToString().Contains("private-123"));
        }));
        test("cli: invalid command is nonzero",()=>
        { using var o=new StringWriter(); using var e=new StringWriter(); Check.Equal(2,ProjectionCli.RunAsync(new[]{"unknown"},o,e).GetAwaiter().GetResult()); });
        test("cli: evaluation diagnostics do not expose input",()=>WithFiles(p=>
        {
            File.WriteAllText(Path.Combine(p,"input.json"),"{\"a\":\"private-payload\"}");
            using var o=new StringWriter(); using var e=new StringWriter(); Check.Equal(1,ProjectionCli.RunAsync(new[]{"preview","--rule",Path.Combine(p,"rule.json"),"--input",Path.Combine(p,"input.json")},o,e).GetAwaiter().GetResult());
            Check.True(!e.ToString().Contains("private-payload")); Check.True(!o.ToString().Contains("private-payload"));
        }));
        test("cli: empty conformance suite fails",()=>WithFiles(p=>
        {
            File.WriteAllText(Path.Combine(p,"suite.json"),"{\"name\":\"empty\",\"cases\":[]}"); using var o=new StringWriter(); using var e=new StringWriter();
            Check.Equal(2,ProjectionCli.RunAsync(new[]{"conformance","--suite",Path.Combine(p,"suite.json")},o,e).GetAwaiter().GetResult());
        }));
        test("cli: conformance mixed status preserves denominator",()=>WithFiles(p=>
        {
            File.WriteAllText(Path.Combine(p,"suite.json"),"{\"name\":\"mixed\",\"cases\":[{\"id\":\"a\",\"expression\":\"1\",\"input\":null,\"expected\":1},{\"id\":\"b\",\"expression\":\"1\",\"input\":null,\"expected\":2}]}");
            using var o=new StringWriter(); using var e=new StringWriter(); Check.Equal(1,ProjectionCli.RunAsync(new[]{"conformance","--suite",Path.Combine(p,"suite.json")},o,e).GetAwaiter().GetResult());
            using var doc=JsonDocument.Parse(o.ToString()); Check.Equal(2,doc.RootElement.GetProperty("Total").GetInt32()); Check.Equal(1,doc.RootElement.GetProperty("Failed").GetInt32());
        }));
        test("cli: benchmark writes measurements",()=>
        {
            using var o=new StringWriter(); using var e=new StringWriter(); Check.Equal(0,ProjectionCli.RunAsync(new[]{"benchmark","--iterations","10","--warmup","1","--rounds","3"},o,e).GetAwaiter().GetResult());
            using var doc=JsonDocument.Parse(o.ToString()); Check.True(doc.RootElement.GetProperty("Scenarios").GetArrayLength()>=3);
        });
    }
}
