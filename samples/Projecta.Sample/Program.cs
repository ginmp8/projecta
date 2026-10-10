using System;
using System.IO;
using System.Text.Json;
using Projecta.Mapping;

var definition = ProjectionDefinition.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "customer.json")));
var projector = new ObjectProjector();
var plan = projector.Compile(definition);
var source = new { id = 123, fullName = "Ana Silva", status = "ACTIVE", address = new { city = "Sao Paulo" }, roles = new[] { "reader", "admin" } };
var dto = projector.Project<CustomerView>(source, plan)!;
Console.WriteLine(JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
// Runtime type resolution is explicit and allowlisted, never Type.GetType from configuration.
var registry = new ProjectionTypeRegistry().Register<CustomerView>("customer-view");
var typed = projector.Project(source, definition, registry);
Console.WriteLine(typed!.GetType().FullName);
Console.WriteLine(projector.ProjectToToken(source, plan).ToFlatString());
public sealed record CustomerView(string Id, string Name, bool IsActive, string City, string[] Roles);
