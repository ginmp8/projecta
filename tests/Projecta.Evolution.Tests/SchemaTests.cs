using Projecta.Contracts;
using Projecta.Json;
using Projecta.Mapping;
using System.Text.Json;

internal static class SchemaTests
{
    internal static void Register(Action<string, Action> test)
    {
        bool Valid(string schema, string data)
        {
            using var d = JsonDocument.Parse(schema);
            return new JsonSchemaContractProvider().Compile(d.RootElement).Validate(JToken.Parse(data)).Count == 0;
        }
        var cases = new (string Name, string Schema, string Data, bool Valid)[]
        {
            ("true schema", "true", "null", true),
            ("false schema", "false", "{}", false),
            ("object type", "{\"type\":\"object\"}", "{}", true),
            ("wrong type", "{\"type\":\"string\"}", "2", false),
            ("nullable type", "{\"type\":[\"string\",\"null\"]}", "null", true),
            ("integer numeric semantics", "{\"type\":\"integer\"}", "1.0", true),
            ("not integer", "{\"type\":\"integer\"}", "1.1", false),
            ("missing required", "{\"required\":[\"name\"]}", "{}", false),
            ("required null exists", "{\"required\":[\"name\"]}", "{\"name\":null}", true),
            ("null fails property type", "{\"properties\":{\"name\":{\"type\":\"string\"}}}", "{\"name\":null}", false),
            ("additional forbidden", "{\"properties\":{\"a\":true},\"additionalProperties\":false}", "{\"a\":1,\"b\":2}", false),
            ("additional schema", "{\"additionalProperties\":{\"type\":\"number\"}}", "{\"a\":1,\"b\":2}", true),
            ("pattern properties", "{\"patternProperties\":{\"^n\":{\"type\":\"number\"}},\"additionalProperties\":false}", "{\"n1\":1}", true),
            ("property names", "{\"propertyNames\":{\"pattern\":\"^a\"}}", "{\"b\":1}", false),
            ("object size", "{\"minProperties\":2}", "{\"a\":1}", false),
            ("dependent required", "{\"dependentRequired\":{\"a\":[\"b\"]}}", "{\"a\":1}", false),
            ("dependent schema", "{\"dependentSchemas\":{\"a\":{\"required\":[\"b\"]}}}", "{\"a\":1,\"b\":2}", true),
            ("array items", "{\"items\":{\"type\":\"integer\"}}", "[1,2,3]", true),
            ("array wrong item", "{\"items\":{\"type\":\"integer\"}}", "[1,\"2\"]", false),
            ("prefix items", "{\"prefixItems\":[{\"type\":\"string\"},{\"type\":\"integer\"}],\"items\":false}", "[\"a\",2]", true),
            ("prefix extra rejected", "{\"prefixItems\":[true],\"items\":false}", "[1,2]", false),
            ("empty prefix allowed", "{\"prefixItems\":[true]}", "[]", true),
            ("contains", "{\"contains\":{\"const\":3}}", "[1,2,3]", true),
            ("contains missing", "{\"contains\":{\"const\":3}}", "[1,2]", false),
            ("contains bounds", "{\"contains\":{\"type\":\"integer\"},\"minContains\":2,\"maxContains\":2}", "[1,2,3]", false),
            ("array length", "{\"minItems\":1,\"maxItems\":2}", "[]", false),
            ("unique numeric values", "{\"uniqueItems\":true}", "[1,1.0]", false),
            ("unicode code points", "{\"minLength\":1,\"maxLength\":1}", "\"\uD83D\uDE00\"", true),
            ("string length", "{\"minLength\":2}", "\"a\"", false),
            ("string regex", "{\"pattern\":\"^[A-Z]{2}$\"}", "\"BR\"", true),
            ("minimum", "{\"minimum\":5}", "4", false),
            ("maximum", "{\"maximum\":5}", "5", true),
            ("exclusive minimum", "{\"exclusiveMinimum\":5}", "5", false),
            ("exclusive maximum", "{\"exclusiveMaximum\":5}", "4.9", true),
            ("decimal multiple exact", "{\"multipleOf\":0.1}", "0.3", true),
            ("decimal not multiple", "{\"multipleOf\":0.1}", "0.31", false),
            ("large bound", "{\"maximum\":1e100}", "1e99", true),
            ("enum", "{\"enum\":[null,1,\"yes\"]}", "\"no\"", false),
            ("const structural", "{\"const\":{\"a\":1,\"b\":2}}", "{\"b\":2,\"a\":1.0}", true),
            ("allOf", "{\"allOf\":[{\"type\":\"integer\"},{\"minimum\":2}]}", "1", false),
            ("anyOf", "{\"anyOf\":[{\"type\":\"string\"},{\"type\":\"number\"}]}", "2", true),
            ("oneOf overlaps", "{\"oneOf\":[{\"type\":\"number\"},{\"type\":\"integer\"}]}", "2", false),
            ("not", "{\"not\":{\"type\":\"string\"}}", "2", true),
            ("conditional then", "{\"if\":{\"type\":\"number\"},\"then\":{\"minimum\":10},\"else\":{\"type\":\"string\"}}", "5", false),
            ("conditional else", "{\"if\":{\"type\":\"number\"},\"then\":{\"minimum\":10},\"else\":{\"type\":\"string\"}}", "\"a\"", true),
            ("local reference", "{\"$defs\":{\"item\":{\"type\":\"integer\"}},\"items\":{\"$ref\":\"#/$defs/item\"}}", "[1,2]", true),
            ("reference siblings enforced", "{\"$defs\":{\"item\":{\"type\":\"integer\"}},\"$ref\":\"#/$defs/item\",\"minimum\":2}", "1", false),
            ("escaped reference", "{\"$defs\":{\"a/b\":{\"type\":\"integer\"}},\"$ref\":\"#/$defs/a~1b\"}", "2", true),
            ("recursive tree", "{\"type\":\"object\",\"properties\":{\"next\":{\"anyOf\":[{\"type\":\"null\"},{\"$ref\":\"#\"}]}}}", "{\"next\":{\"next\":null}}", true)
        };
        foreach (var c in cases) test("schema: " + c.Name, () => Check.Equal(c.Valid, Valid(c.Schema, c.Data)));
        foreach (string invalid in new[]
        {
            "{\"type\":\"unknown\"}", "{\"required\":\"x\"}", "{\"multipleOf\":0}", "{\"minLength\":-1}",
            "{\"$ref\":\"https://private.invalid/schema\"}", "{\"$ref\":\"#/missing\"}", "{\"unevaluatedProperties\":false}",
            "{\"format\":\"email\"}", "{\"unknownAssertion\":true}", "{\"pattern\":\"[\"}", "{\"oneOf\":[]}",
            "{\"properties\":{\"a\":4}}", "{\"uniqueItems\":1}", "{\"type\":[\"string\",\"string\"]}"
        }) test("schema: rejects invalid or unsupported " + invalid, () => Check.Throws<ProjectionConfigurationException>(() =>
        { using var s = JsonDocument.Parse(invalid); new JsonSchemaContractProvider().Compile(s.RootElement); }));
        test("schema: diagnostics carry escaped JSON pointer", () =>
        {
            using var s = JsonDocument.Parse("{\"properties\":{\"a/b~c\":{\"type\":\"string\"}}}");
            var error = new JsonSchemaContractProvider().Compile(s.RootElement).Validate(JToken.Parse("{\"a/b~c\":1}"))[0];
            Check.Equal("/a~1b~0c", error.JsonPointer); Check.Equal("string", error.ExpectedType);
        });
        test("schema: schema is detached from JsonDocument lifetime", () =>
        {
            IProjectionContract contract;
            using (var s = JsonDocument.Parse("{\"type\":\"integer\"}")) contract = new JsonSchemaContractProvider().Compile(s.RootElement);
            Check.Equal(0, contract.Validate(new JValue(1)).Count);
        });
        test("schema: input and output hooks identify phase", () =>
        {
            var p = new ObjectProjector(new() { ContractProvider = new JsonSchemaContractProvider() });
            using var s = JsonDocument.Parse("{\"type\":\"string\"}");
            var input = p.Compile(new() { Expression = "1", InputSchema = s.RootElement });
            var output = p.Compile(new() { Expression = "1", OutputSchema = s.RootElement });
            Check.Equal(ProjectionPhase.InputContract, p.TryProject<int>(2, input).Diagnostics[0].Phase);
            Check.Equal(ProjectionPhase.OutputContract, p.TryProject<int>(null, output).Diagnostics[0].Phase);
        });
        test("schema: input assertions disable selective pruning", () =>
        {
            var p = new ObjectProjector(new() { ContractProvider = new JsonSchemaContractProvider(), EnableSelectiveReading = true });
            var definition = ProjectionDefinition.Parse("{\"expression\":\"a\",\"inputSchema\":{\"required\":[\"b\"]}}");
            Check.Equal(1, p.Project<int>(new { a = 1, b = 2 }, definition));
        });
        test("schema: circular unproductive reference is bounded", () =>
        {
            using var s = JsonDocument.Parse("{\"$ref\":\"#\"}");
            var c = new JsonSchemaContractProvider().Compile(s.RootElement);
            Check.Throws<Projecta.Execution.ExecutionLimitException>(() => c.Validate(new JValue(1)));
        });
        test("schema: cancellation", () =>
        {
            using var s = JsonDocument.Parse("true"); using var c = new CancellationTokenSource(); c.Cancel();
            Check.Throws<OperationCanceledException>(() => new JsonSchemaContractProvider().Compile(s.RootElement).Validate(JValue.CreateNull(), c.Token));
        });
    }
}
