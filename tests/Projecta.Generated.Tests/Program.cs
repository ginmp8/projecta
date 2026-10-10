using System.Text.Json.Serialization;
using Projecta.Generated;

int checks = 0;
for (int i = 0; i < 100; i++)
{
    var source = new Source { Id = i, Name = "name-" + i, Address = i % 2 == 0 ? new Address { City = "city" } : null };
    var result = Mappers.Map(source);
    if (result.Id != i || result.Name != source.Name || result.City != source.Address?.City) throw new InvalidOperationException("Generated mapping mismatch.");
    checks++;
}
var fixedValue = Mappers.Constants(new());
if (fixedValue.Id != 42 || fixedValue.Name != "literal" || fixedValue.City is not null) throw new InvalidOperationException("Generated constants mismatch.");
checks++;
try { Mappers.Map(null!); throw new InvalidOperationException("Null source accepted."); }
catch (ArgumentNullException) { checks++; }
Console.WriteLine($"GENERATED total={checks} passed={checks} failed=0");
return 0;

public sealed class Source
{
    [JsonPropertyName("id")] public int Id { get; init; }
    public string Name { get; init; } = "";
    public Address? Address { get; init; }
}
public sealed class Address { public string? City { get; init; } }
public sealed record Target(long Id, string Name, string? City);
public static partial class Mappers
{
    [StaticProjection("{'Id':id,'Name':Name,'City':Address.City}")]
    public static partial Target Map(Source source);
    [StaticProjection("{'Id':42,'Name':'literal','City':null}")]
    public static partial Target Constants(Source source);
}
