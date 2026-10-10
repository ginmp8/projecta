# Data contracts: projecta-contract-v1

This is a bounded structural validation profile. It is **not a complete JSON Schema 2020-12 implementation**. Schema objects are compiled/frozen; unsupported assertions fail closed. There is no remote resolver, file inclusion, arbitrary code or schema-driven CLR type loading.

```csharp
using Projecta.Contracts;
using Projecta.Mapping;

var projector = new ObjectProjector(new ProjectionOptions
{
    ContractProvider = new JsonSchemaContractProvider()
});
var definition = ProjectionDefinition.Parse("""
{
  "id": "contract-demo",
  "inputSchema": {
    "type": "object", "required": ["name"],
    "properties": { "name": { "type": "string", "minLength": 1 } }
  },
  "outputSchema": {
    "type": "object", "required": ["Name"], "additionalProperties": false,
    "properties": { "Name": { "type": "string" } }
  },
  "fields": { "Name": "name" }
}
""");
var result = projector.TryProject<Person>(new { name = "Ana" }, projector.Compile(definition));
public sealed record Person(string Name);
```

Supplying schemas without a provider is a configuration error, not a silent no-op. Custom providers can implement `IProjectionContractProvider` and `IProjectionContract`; compiled validators must be safe to share with their plan. Input validation runs before evaluation and on the full normalized source. Output validation runs before destination construction.

## Supported assertions

| Area | Keywords |
|---|---|
| General | boolean schemas; `type`, `enum`, `const` |
| Objects | `properties`, `patternProperties`, `additionalProperties`, `required`, `propertyNames`, `minProperties`, `maxProperties`, `dependentRequired`, `dependentSchemas` |
| Arrays | `prefixItems`, `items`, `contains`, `minContains`, `maxContains`, `uniqueItems`, `minItems`, `maxItems` |
| Strings | `minLength`, `maxLength`, `pattern` |
| Numbers | `minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum`, `multipleOf` |
| Composition | `allOf`, `anyOf`, `oneOf`, `not`, `if`, `then`, `else` |
| References | `$defs`, local `$ref` JSON Pointer references and sibling assertions |
| Metadata | `$schema`, `$id`, `$comment`, `title`, `description`, `default`, `examples`, `readOnly`, `writeOnly`, `deprecated` |

Metadata does not mutate data or enforce authorization; `default` is not an insertion operation. Local references are rooted in the supplied schema document; `$id` does not create remote/base-URI resolution. Recursive references are bounded. String length counts Unicode scalar values. Pattern evaluation uses the .NET regular-expression engine with a timeout; not every ECMA-262 regex is portable to this profile.

`format`, `$dynamicRef`, `$dynamicAnchor`, remote references, anchors, `unevaluatedProperties`, `unevaluatedItems`, older-draft tuple/dependency keywords and unknown assertions are rejected. A `$schema` annotation does not override these limitations.

## Limits and errors

Defaults: 1,000,000 schema characters, 4,096 schema nodes, depth 64, 200,000 validation steps, 32 reported errors, regex length 512 and 100 ms regex timeout. Data normalization also applies its configured limits. Equality checks, unique-item comparisons and composition consume the validation budget. Truncating the error list never turns an invalid result into success.

Missing and null are distinct. Type constraints do not implicitly require an optional property. A failed assertion returns a pointer, keyword and type information where known; it does not return the offending value. Numeric schema comparisons use bounded exact decimal text arithmetic where possible, rather than converting every number to a binary double. Extreme numeric representation limits fail explicitly.
