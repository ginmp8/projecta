using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Projecta.EntityFrameworkCore;
using Projecta.Json;
using Projecta.Mapping;
using Projecta.Objects;

int passed = 0, failed = 0;
void Test(string name, Action action) { try { action(); ++passed; Console.WriteLine("PASS " + name); } catch(Exception ex) { ++failed; Console.WriteLine("FAIL " + name + ": " + ex); } }
void Check(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Throws<T>(Action action) where T : Exception { try { action(); } catch(T) { return; } throw new Exception("Expected " + typeof(T).Name); }
Test("proxy source does not lazy load", () =>
{
    using var db = Create(); var customer = db.Customers.Single();
    Check(!db.Entry(customer).Collection(c => c.Orders).IsLoaded);
    var p = new ObjectProjector(sourceAdapters: new[] { new EfCoreSourceAdapter(db) });
    var result = p.ProjectToToken(customer, new() { Expression = "{'Name':Name}" });
    Check((string)((JObject)result).Properties["Name"] == "Ana");
    Check(!db.Entry(customer).Collection(c => c.Orders).IsLoaded);
    Check(db.ChangeTracker.LazyLoadingEnabled && db.ChangeTracker.AutoDetectChangesEnabled);
});
Test("root collection containing proxies", () =>
{
    using var db = Create(); var customers = db.Customers.ToList();
    var p = new ObjectProjector(sourceAdapters: new[] { new EfCoreSourceAdapter(db) });
    Check(p.Project<int>(customers, new() { Expression = "$count($)" }) == 1);
    Check(!db.Entry(customers[0]).Collection(c => c.Orders).IsLoaded);
});
Test("explicit loaded navigation", () =>
{
    using var db = Create(); var customer = db.Customers.Include(c => c.Orders).Single();
    var adapter = new EfCoreSourceAdapter(db, new() { LoadedNavigations = new HashSet<string> { "Orders" } });
    var p = new ObjectProjector(sourceAdapters: new[] { adapter });
    Check(p.Project<int>(customer, new() { Expression = "$count(Orders)" }) == 1);
});
Test("requested unloaded navigation fails without loading", () =>
{
    using var db = Create(); var customer = db.Customers.Single();
    var p = new ObjectProjector(sourceAdapters: new[] { new EfCoreSourceAdapter(db, new() { LoadedNavigations = new HashSet<string> { "Orders" } }) });
    Throws<ObjectReadException>(() => p.ProjectToToken(customer, new() { Expression = "$" }));
    Check(!db.Entry(customer).Collection(c => c.Orders).IsLoaded && db.ChangeTracker.LazyLoadingEnabled);
});
Test("detached mapped entity rejected", () =>
{
    using var db = Create(); var p = new ObjectProjector(sourceAdapters: new[] { new EfCoreSourceAdapter(db) });
    Throws<ObjectReadException>(() => p.ProjectToToken(new Customer(), new() { Expression = "$" }));
});
Test("allowlisted scalar update", () =>
{
    using var db = Create(); var customer = db.Customers.Single();
    new TrackedEntityUpdater(db).Apply(customer, (JObject)JToken.Parse("{'Name':'Changed'}"), new[] { "Name" });
    Check(customer.Name == "Changed");
});
Test("key update prohibited before mutation", () =>
{
    using var db = Create(); var customer = db.Customers.Single(); var id = customer.Id;
    Throws<ProjectionMaterializationException>(() => new TrackedEntityUpdater(db).Apply(customer, (JObject)JToken.Parse("{'Id':999}"), new[] { "Id" }));
    Check(customer.Id == id);
});
Test("foreign key update prohibited", () =>
{
    using var db = Create(); var order = db.Orders.Single();
    Throws<ProjectionMaterializationException>(() => new TrackedEntityUpdater(db).Apply(order, (JObject)JToken.Parse("{'CustomerId':999}"), new[] { "CustomerId" }));
});
Test("unpermitted update fails", () =>
{
    using var db = Create(); var customer = db.Customers.Single();
    Throws<ProjectionMaterializationException>(() => new TrackedEntityUpdater(db).Apply(customer, (JObject)JToken.Parse("{'Name':'bad'}"), Array.Empty<string>()));
    Check(customer.Name == "Ana");
});
Console.WriteLine($"RESULT passed={passed} failed={failed}"); return failed == 0 && passed == 9 ? 0 : 1;

TestDb Create()
{
    var options = new DbContextOptionsBuilder<TestDb>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).UseLazyLoadingProxies().Options;
    var db = new TestDb(options); db.Customers.Add(new Customer { Name = "Ana", Orders = new List<Order> { new() { Amount = 10m } } }); db.SaveChanges(); db.ChangeTracker.Clear(); return db;
}
public sealed class TestDb(DbContextOptions<TestDb> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>(); public DbSet<Order> Orders => Set<Order>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Customer>().HasMany(c => c.Orders).WithOne(o => o.Customer).HasForeignKey(o => o.CustomerId);
}
public class Customer { public int Id { get; set; } public string Name { get; set; } = ""; public virtual ICollection<Order> Orders { get; set; } = new List<Order>(); }
public class Order { public int Id { get; set; } public decimal Amount { get; set; } public int CustomerId { get; set; } public virtual Customer Customer { get; set; } = null!; }
