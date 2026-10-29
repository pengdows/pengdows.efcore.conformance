using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class QuerySetOperatorTests
{
    [Fact]
    [BehavioralCoverage("EF10-QUERY-CONTAINS-SYNC", "System.Linq.Queryable.Contains")]
    public async Task Contains_FindsAndRejectsValuesSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var ages = context.People.Where(person => person.Name.StartsWith(batch)).Select(person => person.Age);

        Assert.True(ages.Contains(20));
        Assert.False(ages.Contains(40));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-CONTAINS-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ContainsAsync")]
    public async Task ContainsAsync_FindsAndRejectsValues()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var ages = context.People.Where(person => person.Name.StartsWith(batch)).Select(person => person.Age);

        Assert.True(await ages.ContainsAsync(20, TestContext.Current.CancellationToken));
        Assert.False(await ages.ContainsAsync(40, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-ALL-SYNC", "System.Linq.Queryable.All")]
    public async Task All_EvaluatesPredicateAcrossAllRowsSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var people = context.People.Where(person => person.Name.StartsWith(batch));

        Assert.False(people.All(person => person.Age >= 20));
        Assert.True(people.All(person => person.Age >= 10));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-ALL-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AllAsync")]
    public async Task AllAsync_EvaluatesPredicateAcrossAllRows()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var people = context.People.Where(person => person.Name.StartsWith(batch));

        Assert.False(await people.AllAsync(person => person.Age >= 20, TestContext.Current.CancellationToken));
        Assert.True(await people.AllAsync(person => person.Age >= 10, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-LONGCOUNT-SYNC", "System.Linq.Queryable.LongCount")]
    public async Task LongCount_ReturnsMatchingRowCountSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        Assert.Equal(3L, context.People.LongCount(person => person.Name.StartsWith(batch)));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-LONGCOUNT-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.LongCountAsync")]
    public async Task LongCountAsync_ReturnsMatchingRowCount()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        Assert.Equal(3L, await context.People.LongCountAsync(
            person => person.Name.StartsWith(batch),
            TestContext.Current.CancellationToken));
    }

    private static async Task SeedAsync(string batch)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.AddRange(
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-ten", Age = 10 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-twenty", Age = 20 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-thirty", Age = 30 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
