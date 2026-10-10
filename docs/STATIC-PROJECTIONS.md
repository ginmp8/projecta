# Restricted static projections

Dynamic JSONata, provider queries and generated C# are different execution contracts. Unsupported constructs are rejected; neither static path secretly falls back to full in-memory JSONata evaluation.

## IQueryable: scalar-select-v1

```csharp
using Projecta.Mapping;
using Projecta.Queryable;

var definition = new ProjectionDefinition
{
    Expression = "{'Id':Id,'Name':Name}"
};
IQueryable<CustomerView> query = customers.ProjectTo<Customer, CustomerView>(definition);
// The caller applies provider-specific async execution and cancellation here.
```

`QueryProjection.Build<TSource,TTarget>` returns a LINQ expression; `ProjectTo` appends `Queryable.Select`. Supported forms are literal-key objects/fields whose values are static scalar property paths or constants. Constructor/record/init-property binding, `JsonPropertyName`, required destination members, null propagation and selected safe widening conversions are supported.

Functions, aggregations, arithmetic, dynamic indexing, collections, arbitrary nested object constructors, runtime schemas and runtime target aliases are rejected. The translator never calls `AsEnumerable`, compiles a query delegate or accesses the database. The underlying provider still owns translation and may reject a constructed expression. There is **no promise of SQL translation for every provider**.

In-memory expression behavior was executed. A SQLite provider test is supplied in `tests/Projecta.Relational.Tests`, checking generated SQL, selected columns, single-query execution and rejection before database access; its exact package restore and execution were blocked in the delivery environment. Run it before enabling the feature for a real provider. Provider translation is not authorization; authorize data and fields before building a projection.

## Generator: static-scalar-v1

Reference `Projecta.SourceGeneration` as a private analyzer package. It produces the attribute and implementation during compilation:

```xml
<PackageReference Include="Projecta.SourceGeneration" PrivateAssets="all" />
```

The version belongs in the consumer's central package file when central management is enabled.

```csharp
using Projecta.Generated;

public sealed class Customer
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
}
public sealed record CustomerView(long Id, string Name);

public static partial class CustomerMappings
{
    [StaticProjection("{'Id':Id,'Name':Name}")]
    public static partial CustomerView ToView(Customer source);
}
```

Declarations must be non-generic public/internal static partial methods with one ordinary source parameter, inside a top-level non-generic static partial class. The output uses direct property reads and object construction, not runtime interpretation/reflection. Each mapped value is evaluated once; path null checks can still read intermediate getters more than once, so use normal side-effect-free DTO accessors.

The supported scalar subset covers literal strings, booleans, null, supported numbers and scalar property paths with constructor/init binding. Functions, array operators, general expressions and runtime configuration are not compiled. Required init properties remain initialized even when constructor binding does not declare `SetsRequiredMembers`. `JsonIgnore(Never)` preserves members; conditional omission is outside this profile. Non-nullable reference destinations cannot silently accept nullable paths.

Diagnostics: `PJG001` declaration, `PJG002` expression/profile/complexity, `PJG003` incompatible source/destination binding. Unsupported expressions fail the build; they do not emit dummy methods.

## Compiler and AOT boundaries

The analyzer targets **.NET 10 compiler hosts** and was tested with SDK 10.0.302. SDK compiler assemblies are build-time references, not copied into runtime libraries. The package contains its analyzer and parser dependency. This is not a .NET Standard analyzer for older IDEs.

The generated mapper itself does not require runtime reflection. However, the full Projecta interpreter/object reader, custom converters, adapters and arbitrary applications are **not certified trimming/Native AOT compatible**. `ProjectWithTypeInfo` only establishes an explicit destination metadata path. Native AOT publishing and execution need their own tests and were not performed for this delivery.
