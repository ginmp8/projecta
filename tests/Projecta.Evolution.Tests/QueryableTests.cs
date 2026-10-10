using System.Linq.Expressions;
using System.Text.Json.Serialization;
using Projecta.Mapping;
using Projecta.Queryable;

internal static class QueryableTests
{
    internal static void Register(Action<string, Action> test)
    {
        ProjectionDefinition Definition() => new() { Fields = new() { ["Id"] = "id", ["Name"] = "Name", ["City"] = "Address.City" } };
        test("query: scalar member projection", () =>
        {
            var mapping = QueryProjection.Build<QuerySource,QueryTarget>(Definition()).Compile();
            Check.Equal(new QueryTarget(3, "Ana", "X"), mapping(new QuerySource { Id=3, Name="Ana", Address=new() { City="X" } }));
        });
        test("query: null navigation becomes target default", () =>
        { var mapping=QueryProjection.Build<QuerySource,QueryTarget>(Definition()).Compile(); Check.Equal<string?>(null, mapping(new()).City); });
        test("query: provider expression is Select, without invocation or enumerable fallback", () =>
        {
            var source = new[] { new QuerySource { Id=2 } }.AsQueryable();
            var result = source.ProjectTo<QuerySource, QueryTarget>(Definition());
            Check.Equal("Select", ((MethodCallExpression)result.Expression).Method.Name);
            Check.Equal(typeof(System.Linq.Queryable), ((MethodCallExpression)result.Expression).Method.DeclaringType);
            Check.Equal(2L, result.Single().Id); Check.True(!result.Expression.ToString().Contains("Invoke("));
        });
        test("query: constants and object expression", () =>
        { var m=QueryProjection.Build<QuerySource,QueryTarget>(new() { Expression="{'Id':42,'Name':'static','City':null}" }).Compile(); Check.Equal(new QueryTarget(42,"static",null), m(new())); });
        test("query: writable destination", () =>
        { var m=QueryProjection.Build<QuerySource,QueryMutable>(new() { Fields=new() { ["Id"]="id" } }).Compile(); Check.Equal(2L,m(new() { Id=2 }).Id); });
        foreach (string expression in new[] { "$sum(values)", "values[0]", "Name & 'x'", "id+1", "$tenant", "*", "[id]", "missing" })
            test("query: unsupported expression " + expression, () => Check.Throws<ProjectionTranslationException>(() => QueryProjection.Build<QuerySource,QueryMutable>(new() { Fields=new() { ["Id"]=expression } })));
        test("query: contracts cannot be silently skipped", () =>
        { using var s=System.Text.Json.JsonDocument.Parse("true"); Check.Throws<ProjectionTranslationException>(() => QueryProjection.Build<QuerySource,QueryMutable>(new() { Fields=new() { ["Id"]="id" },InputSchema=s.RootElement })); });
        test("query: unknown target member", () => Check.Throws<ProjectionTranslationException>(() => QueryProjection.Build<QuerySource,QueryMutable>(new() { Fields=new() { ["Typo"]="id" } })));
        test("query: narrowing rejected", () => Check.Throws<ProjectionTranslationException>(() => QueryProjection.Build<QueryTarget,QuerySource>(new() { Fields=new() { ["id"]="Id" } })));
        test("query: required destination member", () => Check.Throws<ProjectionTranslationException>(() => QueryProjection.Build<QuerySource,QueryRequired>(new() { Fields=new() { ["Id"]="id" } })));
        test("query: field diagnosis", () =>
        { var e=Check.Throws<ProjectionTranslationException>(() => QueryProjection.Build<QuerySource,QueryMutable>(new() { Fields=new() { ["Id"]="missing" } })); Check.Equal("Id",e.Field); });
    }
}
public sealed class QuerySource
{
    [JsonPropertyName("id")] public int Id { get; init; }
    public string? Name { get; init; }
    public QueryAddress? Address { get; init; }
    public int[] values { get; init; }=Array.Empty<int>();
}
public sealed class QueryAddress { public string? City { get; init; } }
public sealed record QueryTarget(long Id, string? Name, string? City);
public sealed class QueryMutable { public long Id { get; set; } }
public sealed class QueryRequired { public long Id { get; set; } public required string Name { get; init; } }
