using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Projecta.SourceGeneration;
using System.Reflection;
using System.Runtime.Loader;

internal static class GeneratorTests
{
    private const string Good = """
    #nullable enable
    using Projecta.Generated;
    public sealed class Source { [System.Text.Json.Serialization.JsonPropertyName("id")] public int Id {get;init;} public string Name {get;init;} = ""; }
    public sealed record Target(long Id, string Name);
    public static partial class Mapping
    {
      [StaticProjection("{'Id':id,'Name':Name}")]
      public static partial Target Convert(Source source);
    }
    """;
    internal static void Register(Action<string, Action> test)
    {
        test("generator: emits mapping with no runtime interpreter", () =>
        {
            var (compilation,result) = Generate(Good);
            Check.True(result.GeneratedTrees.Length >= 2);
            Check.True(!result.Diagnostics.Any(d=>d.Severity==DiagnosticSeverity.Error));
            var errors=compilation.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
            if(errors.Length>0) throw new Exception(string.Join("\n",errors.Select(x=>x.ToString())));
            Check.True(result.GeneratedTrees.SelectMany(t=>t.GetRoot().DescendantNodes())
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MemberAccessExpressionSyntax>()
                .Any(n=>n.Name.Identifier.ValueText=="Id" && n.Expression is Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax id && id.Identifier.ValueText=="source"));
            Check.True(result.GeneratedTrees.All(t=>!t.ToString().Contains("ProjectaQuery")));
        });
        test("generator: generated mapper actually executes", () =>
        {
            var (compilation,_) = Generate(Good);
            using var stream=new MemoryStream(); var result=compilation.Emit(stream);
            if(!result.Success) throw new Exception(string.Join("\n",result.Diagnostics));
            stream.Position=0; var context=new AssemblyLoadContext("generated-check",isCollectible:true);
            try
            {
                var assembly=context.LoadFromStream(stream); var type=assembly.GetType("Source")!; var source=Activator.CreateInstance(type)!;
                type.GetProperty("Id")!.SetValue(source,7); type.GetProperty("Name")!.SetValue(source,"Ana");
                var output=assembly.GetType("Mapping")!.GetMethod("Convert")!.Invoke(null,new[] {source})!;
                Check.Equal(7L,(long)output.GetType().GetProperty("Id")!.GetValue(output)!);
            }
            finally {context.Unload();}
        });
        test("generator: rejects unsupported expression", () =>
        { var (_,r)=Generate(Good.Replace("{'Id':id,'Name':Name}","{'Id':$sum(id),'Name':Name}")); Check.True(r.Diagnostics.Any(x=>x.Id=="PJG002")); });
        test("generator: rejects invalid signature", () =>
        { var (_,r)=Generate(Good.Replace("Convert(Source source)","Convert(Source source, int extra)")); Check.True(r.Diagnostics.Any(x=>x.Id=="PJG001")); });
        test("generator: rejects missing target property", () =>
        { var (_,r)=Generate(Good.Replace("'Name':Name","'Typo':Name")); Check.True(r.Diagnostics.Any(x=>x.Id=="PJG003")); });
        test("generator: Never ignore condition preserves source member", () =>
        {
            var (c, r) = Generate(Good.Replace("public int Id", "[System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.Never)] public int Id"));
            Check.True(!r.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
            Check.True(!c.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error));
        });
        test("generator: excessive expression reports a bounded diagnostic", () =>
        {
            var (_, r) = Generate(Good.Replace("{'Id':id,'Name':Name}", new string('(', 200) + "1" + new string(')', 200)));
            Check.True(r.Diagnostics.Any(d => d.Id == "PJG002"));
            Check.True(!r.Diagnostics.Any(d => d.Id == "CS8785" || d.Id == "AD0001"));
        });
        test("generator: required init properties remain valid with constructor binding", () =>
        {
            var code = Good.Replace("public sealed record Target(long Id, string Name);", "public sealed class Target { public required long Id {get;init;} public required string Name {get;init;} public Target(long id, string name) {Id=id;Name=name;} }");
            var (c, r) = Generate(code);
            Check.True(!r.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
            Check.True(!c.GetDiagnostics().Any(d => d.Severity == DiagnosticSeverity.Error));
        });
        test("generator: deterministic output", () =>
        { var (_,a)=Generate(Good); var (_,b)=Generate(Good); Check.Equal(string.Join("\n",a.GeneratedTrees),string.Join("\n",b.GeneratedTrees)); });
    }
    private static (Compilation,GeneratorDriverRunResult) Generate(string source)
    {
        var references=((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(p=>MetadataReference.CreateFromFile(p));
        var compilation=CSharpCompilation.Create("GeneratedProjection"+Guid.NewGuid().ToString("N"),
            new[] {CSharpSyntaxTree.ParseText(source,new CSharpParseOptions(LanguageVersion.CSharp14))},references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,nullableContextOptions:NullableContextOptions.Enable));
        GeneratorDriver driver=CSharpGeneratorDriver.Create(new[] {new StaticProjectionGenerator().AsSourceGenerator()},parseOptions:new CSharpParseOptions(LanguageVersion.CSharp14));
        driver=driver.RunGeneratorsAndUpdateCompilation(compilation,out var output,out _);
        return(output,driver.GetRunResult());
    }
}
