using Projecta;
using Projecta.Contracts;
using Projecta.Documents;
using Projecta.Execution;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.Objects;
using System.Text.Json;

internal static class AdversarialTests
{
    internal static void Register(Action<string, Action> test)
    {
        test("pipeline: wrong version type is a configuration failure", () =>
        {
            foreach (string value in new[] { "\"1\"", "null", "true", "[]", "{}" })
                Check.Throws<ProjectionConfigurationException>(() => DocumentPipeline.Parse("{\"version\":" + value + ",\"steps\":[]}"));
        });
        test("fuzz: 600 deterministic bounded parser/evaluator inputs", () =>
        {
            var random=new Random(7319); const string alphabet="abc012+*-?:[]{}(),. $='";
            for(int i=0;i<600;i++)
            {
                string expression=new(Enumerable.Range(0,random.Next(1,80)).Select(_=>alphabet[random.Next(alphabet.Length)]).ToArray());
                try {new ProjectaQuery(expression).Eval(JValue.CreateNull(),new EvaluationLimits {MaxSteps=1000,MaxAllocatedTokens=1000,MaxRangeLength=100,Timeout=TimeSpan.FromMilliseconds(100)});}
                catch(JsonataException) { }
                catch(ExecutionLimitException) { }
            }
        });
        test("fuzz: parser nesting remains bounded", () =>
        {
            foreach(int depth in new[] { 130, 200, 400 })
                Check.Throws<ExecutionLimitException>(()=>new ProjectaQuery(new string('(',depth)+"1"+new string(')',depth)));
        });
        test("fuzz: no cross-call environment after failures",()=>
        {
            var p=new ObjectProjector(new() {Evaluation=new() {MaxSteps=50}}); var plan=p.Compile(new() {Expression="$value + a"});
            Parallel.For(0,250,i=>
            {
                var bad=p.TryProject<int>(new {a="bad"},plan,new(new Dictionary<string,object?> { ["value"]=i })); Check.True(!bad.Success);
                Check.Equal(i+1,p.TryProject<int>(new {a=1},plan,new(new Dictionary<string,object?> { ["value"]=i })).Value);
            });
        });
        test("schema: error cap does not accept invalid data",()=>
        {
            using var schema=JsonDocument.Parse("{\"items\":{\"type\":\"integer\"}}");
            var contract=new JsonSchemaContractProvider(new() {MaxErrors=2}).Compile(schema.RootElement);
            Check.Equal(2,contract.Validate(JToken.Parse("[\"x\",\"y\",\"z\"]")).Count);
        });
        test("schema: referenced annotation cannot hide unknown assertions",()=>
        {
            using var s=JsonDocument.Parse("{\"default\":{\"unknownAssertion\":true},\"$ref\":\"#/default\"}");
            Check.Throws<ProjectionConfigurationException>(()=>new JsonSchemaContractProvider().Compile(s.RootElement));
        });
        test("schema: evaluation cost budget applies to equality",()=>
        {
            using var s=JsonDocument.Parse("{\"uniqueItems\":true}");
            var c=new JsonSchemaContractProvider(new() {MaxSteps=20}).Compile(s.RootElement);
            Check.Throws<ExecutionLimitException>(()=>c.Validate(new ObjectReader().Read(Enumerable.Range(0,100).ToArray())));
        });
        test("patch: bounded operation growth",()=>
        {
            var p=JsonPatchDocument.Parse("[{\"op\":\"copy\",\"from\":\"/a\",\"path\":\"/b\"}]",new() {Data=new() {MaxNodes=5}});
            var source=JToken.Parse("{\"a\":[1,2]}"); Check.Throws<ObjectReadException>(()=>p.Apply(source)); Check.Json("{\"a\":[1,2]}",source);
        });
        test("pipeline: failing later stage does not expose mutations",()=>
        {
            var p=DocumentPipeline.Parse("{\"version\":1,\"steps\":[{\"op\":\"default\",\"path\":\"/x\",\"value\":1},{\"op\":\"rename\",\"from\":\"/missing\",\"path\":\"/z\"}]}");
            var source=JToken.Parse("{}"); Check.Throws<DocumentTransformException>(()=>p.Execute(source)); Check.Json("{}",source);
        });
        test("pipeline: duplicate or unknown operation member rejected",()=>
        {
            foreach(string value in new[]{"{\"version\":1,\"steps\":[],\"steps\":[]}","{\"version\":1,\"steps\":[{\"op\":\"omit\",\"path\":\"/a\",\"typo\":true}]}"})
                Check.Throws<ProjectionConfigurationException>(()=>DocumentPipeline.Parse(value));
        });
    }
}
