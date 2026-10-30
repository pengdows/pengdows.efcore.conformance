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

    [Fact]
    [BehavioralCoverage("EF10-QUERY-ANY-SYNC", "System.Linq.Queryable.Any")]
    public async Task Any_ReportsWhetherMatchingRowsExistSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var missingName = Guid.NewGuid().ToString("N");
        Assert.True(context.People.Any(person => person.Name.StartsWith(batch) && person.Age == 20));
        Assert.False(context.People.Any(person => person.Name.StartsWith(batch) && person.Age == 40));
        Assert.False(context.People.Any(person => person.Name == missingName));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-ANY-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync")]
    public async Task AnyAsync_ReportsWhetherMatchingRowsExist()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var missingName = Guid.NewGuid().ToString("N");
        Assert.True(await context.People.AnyAsync(
            person => person.Name.StartsWith(batch) && person.Age == 20,
            TestContext.Current.CancellationToken));
        Assert.False(await context.People.AnyAsync(
            person => person.Name.StartsWith(batch) && person.Age == 40,
            TestContext.Current.CancellationToken));
        Assert.False(await context.People.AnyAsync(
            person => person.Name == missingName,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-DISTINCT-SYNC", "System.Linq.Queryable.Distinct", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.ToArray")]
    public async Task Distinct_ReturnsUniqueValuesSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedSetOperatorRowsAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = AgeQuery(context, batch).Distinct().OrderBy(age => age).ToArray();

        Assert.Equal(new[] { 10, 20, 30 }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-DISTINCT-ASYNC", "System.Linq.Queryable.Distinct", "System.Linq.Queryable.OrderBy", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync")]
    public async Task Distinct_ReturnsUniqueValuesAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedSetOperatorRowsAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = await AgeQuery(context, batch).Distinct().OrderBy(age => age)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { 10, 20, 30 }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-UNION-SYNC", "System.Linq.Queryable.Union", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.ToArray")]
    public async Task Union_ReturnsEachValueFromEitherSequenceSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedSetOperatorRowsAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var ages = AgeQuery(context, batch);
        var actual = ages.Where(age => age <= 20)
            .Union(ages.Where(age => age >= 20))
            .OrderBy(age => age)
            .ToArray();

        Assert.Equal(new[] { 10, 20, 30 }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-UNION-ASYNC", "System.Linq.Queryable.Union", "System.Linq.Queryable.OrderBy", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync")]
    public async Task Union_ReturnsEachValueFromEitherSequenceAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedSetOperatorRowsAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var ages = AgeQuery(context, batch);
        var actual = await ages.Where(age => age <= 20)
            .Union(ages.Where(age => age >= 20))
            .OrderBy(age => age)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { 10, 20, 30 }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-INTERSECT-SYNC", "System.Linq.Queryable.Intersect", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.ToArray")]
    public async Task Intersect_ReturnsValuesPresentInBothSequencesSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedSetOperatorRowsAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var ages = AgeQuery(context, batch);
        var actual = ages.Where(age => age <= 20)
            .Intersect(ages.Where(age => age >= 20))
            .OrderBy(age => age)
            .ToArray();

        Assert.Equal(new[] { 20 }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-INTERSECT-ASYNC", "System.Linq.Queryable.Intersect", "System.Linq.Queryable.OrderBy", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync")]
    public async Task Intersect_ReturnsValuesPresentInBothSequencesAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedSetOperatorRowsAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var ages = AgeQuery(context, batch);
        var actual = await ages.Where(age => age <= 20)
            .Intersect(ages.Where(age => age >= 20))
            .OrderBy(age => age)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { 20 }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-EXCEPT-SYNC", "System.Linq.Queryable.Except", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.ToArray")]
    public async Task Except_ReturnsValuesMissingFromSecondSequenceSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedSetOperatorRowsAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var ages = AgeQuery(context, batch);
        var actual = ages.Where(age => age <= 20)
            .Except(ages.Where(age => age >= 20))
            .OrderBy(age => age)
            .ToArray();

        Assert.Equal(new[] { 10 }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-EXCEPT-ASYNC", "System.Linq.Queryable.Except", "System.Linq.Queryable.OrderBy", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync")]
    public async Task Except_ReturnsValuesMissingFromSecondSequenceAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedSetOperatorRowsAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var ages = AgeQuery(context, batch);
        var actual = await ages.Where(age => age <= 20)
            .Except(ages.Where(age => age >= 20))
            .OrderBy(age => age)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { 10 }, actual);
    }

    private static IQueryable<int> AgeQuery(ConformanceDbContext context, string batch) =>
        context.People.Where(person => person.Name.StartsWith(batch)).Select(person => person.Age);

    private static async Task SeedSetOperatorRowsAsync(string batch)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.AddRange(
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-ten-a", Age = 10 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-ten-b", Age = 10 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-twenty", Age = 20 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-thirty", Age = 30 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
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
