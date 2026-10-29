using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class RawSqlTests
{
    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-FROM-ASYNC", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSqlInterpolated", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task FromSqlInterpolated_ReturnsParameterizedResult()
    {
        var id = Guid.NewGuid();
        const string value = "O'Brien; DROP TABLE test";
        await SeedAsync(id, value);

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = await context.People
            .FromSqlInterpolated($"SELECT * FROM ef10conformancepeople WHERE id = {id}")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(value, actual.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-FROM-SYNC", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSqlInterpolated", "System.Linq.Queryable.Single")]
    public async Task FromSqlInterpolated_ReturnsResultSynchronously()
    {
        var id = Guid.NewGuid();
        const string value = "Sync O'Brien";
        await SeedAsync(id, value);

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = context.People
            .FromSqlInterpolated($"SELECT * FROM ef10conformancepeople WHERE id = {id}")
            .Single();

        Assert.Equal(value, actual.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-COMMAND-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync")]
    public async Task ExecuteSqlInterpolatedAsync_UpdatesPersistedValues()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Before raw update");

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            Assert.Equal(1, await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ef10conformancepeople SET name = {"After raw update"} WHERE id = {id}",
                TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var name = await verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("After raw update", name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-COMMAND-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolated")]
    public async Task ExecuteSqlInterpolated_UpdatesPersistedValuesSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Before sync raw update");

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            Assert.Equal(1, context.Database.ExecuteSqlInterpolated(
                $"UPDATE ef10conformancepeople SET name = {"After sync raw update"} WHERE id = {id}"));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var name = await verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("After sync raw update", name);
    }

    private static async Task SeedAsync(Guid id, string name)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = name, Age = 29 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
