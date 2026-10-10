using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Projecta.Mapping;
using Projecta.Queryable;

await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
var counter = new CommandCounter();
var options = new DbContextOptionsBuilder<TestDb>().UseSqlite(connection).AddInterceptors(counter).Options;
await using var db = new TestDb(options);
await db.Database.EnsureCreatedAsync();
db.Customers.Add(new Customer { Id = 1, Name = "Ana", LargeUnused = new string('x', 1024) });
await db.SaveChangesAsync(); db.ChangeTracker.Clear(); counter.Readers = 0;
var definition = new ProjectionDefinition { Fields = new() { ["Id"] = "Id", ["Name"] = "Name" } };
var query = db.Customers.AsNoTracking().ProjectTo<Customer, CustomerView>(definition);
if (counter.Readers != 0) throw new InvalidOperationException("Query executed while building projection.");
string sql = query.ToQueryString();
if (sql.Contains("LargeUnused", StringComparison.Ordinal)) throw new InvalidOperationException("Unused column was selected.");
var rows = await query.ToListAsync();
if (rows.Count != 1 || rows[0] != new CustomerView(1, "Ana") || counter.Readers != 1 || db.ChangeTracker.Entries().Any())
    throw new InvalidOperationException("Relational projection contract failed.");
counter.Readers = 0;
try
{
    db.Customers.ProjectTo<Customer, CustomerView>(definition with { Fields = new() { ["Id"] = "Id", ["Name"] = "$uppercase(Name)" } });
    throw new InvalidOperationException("Unsupported expression was accepted.");
}
catch (ProjectionTranslationException) { }
if (counter.Readers != 0) throw new InvalidOperationException("Rejected projection touched the database.");
Console.WriteLine("RELATIONAL passed=5 failed=0 provider=SQLite");
return 0;

public sealed class Customer { public int Id { get; set; } public string Name { get; set; } = ""; public string LargeUnused { get; set; } = ""; }
public sealed record CustomerView(int Id, string Name);
public sealed class TestDb(DbContextOptions<TestDb> options) : DbContext(options)
{ public DbSet<Customer> Customers => Set<Customer>(); }
public sealed class CommandCounter : DbCommandInterceptor
{
    public int Readers;
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    { Readers++; return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { Readers++; return ValueTask.FromResult(result); }
}
