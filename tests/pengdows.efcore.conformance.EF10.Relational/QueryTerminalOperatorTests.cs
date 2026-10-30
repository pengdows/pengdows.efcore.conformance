using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class QueryTerminalOperatorTests
{
    [Fact]
    [BehavioralCoverage("EF10-QUERY-FIRST-SYNC", "System.Linq.Queryable.First")]
    public async Task First_ReturnsFirstMatchingEntitySynchronously()
    {
        var (batch, expected, _, _) = await SeedThreePeopleAsync();

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = context.People
            .Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Age)
            .First();

        Assert.Equal(expected.Id, actual.Id);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-FIRST-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync")]
    public async Task FirstAsync_ReturnsFirstMatchingEntity()
    {
        var (batch, expected, _, _) = await SeedThreePeopleAsync();

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = await context.People
            .Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Age)
            .FirstAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected.Id, actual.Id);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-FIRST-EMPTY-SYNC", "System.Linq.Queryable.First")]
    public async Task First_ThrowsForEmptyResultSynchronously()
    {
        var missingName = Guid.NewGuid().ToString("N");
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.Throws<InvalidOperationException>(() => context.People.First(person => person.Name == missingName));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-FIRST-EMPTY-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstAsync")]
    public async Task FirstAsync_ThrowsForEmptyResult()
    {
        var missingName = Guid.NewGuid().ToString("N");
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.People.FirstAsync(
            person => person.Name == missingName,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-FIRST-OR-DEFAULT-SYNC", "System.Linq.Queryable.FirstOrDefault")]
    public async Task FirstOrDefault_ReturnsDefaultForEmptyResultSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        var actual = context.People.FirstOrDefault(person => person.Name == batch);

        Assert.Null(actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-FIRST-OR-DEFAULT-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.FirstOrDefaultAsync")]
    public async Task FirstOrDefaultAsync_ReturnsDefaultForEmptyResult()
    {
        var batch = Guid.NewGuid().ToString("N");
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        var actual = await context.People.FirstOrDefaultAsync(
            person => person.Name == batch,
            TestContext.Current.CancellationToken);

        Assert.Null(actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-SINGLE-SYNC", "System.Linq.Queryable.Single")]
    public async Task Single_ReturnsOnlyMatchingEntitySynchronously()
    {
        var (_, _, expected, _) = await SeedThreePeopleAsync();

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = context.People.Single(person => person.Name == expected.Name);

        Assert.Equal(expected.Id, actual.Id);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-SINGLE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task SingleAsync_ReturnsOnlyMatchingEntity()
    {
        var (_, _, expected, _) = await SeedThreePeopleAsync();

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = await context.People.SingleAsync(
            person => person.Name == expected.Name,
            TestContext.Current.CancellationToken);

        Assert.Equal(expected.Id, actual.Id);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-SINGLE-MULTIPLE-SYNC", "System.Linq.Queryable.Single")]
    public async Task Single_ThrowsForMultipleMatchingEntitiesSynchronously()
    {
        var (batch, _, _, _) = await SeedThreePeopleAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        Assert.Throws<InvalidOperationException>(() => context.People.Single(person => person.Name.StartsWith(batch)));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-SINGLE-MULTIPLE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task SingleAsync_ThrowsForMultipleMatchingEntities()
    {
        var (batch, _, _, _) = await SeedThreePeopleAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.People.SingleAsync(
            person => person.Name.StartsWith(batch),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-SINGLE-OR-DEFAULT-SYNC", "System.Linq.Queryable.SingleOrDefault")]
    public async Task SingleOrDefault_ReturnsDefaultForEmptyResultSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        var actual = context.People.SingleOrDefault(person => person.Name == batch);

        Assert.Null(actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-SINGLE-OR-DEFAULT-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleOrDefaultAsync")]
    public async Task SingleOrDefaultAsync_ReturnsDefaultForEmptyResult()
    {
        var batch = Guid.NewGuid().ToString("N");
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        var actual = await context.People.SingleOrDefaultAsync(
            person => person.Name == batch,
            TestContext.Current.CancellationToken);

        Assert.Null(actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-SINGLE-OR-DEFAULT-MULTIPLE-SYNC", "System.Linq.Queryable.SingleOrDefault")]
    public async Task SingleOrDefault_ThrowsWhenMultipleRowsMatchSynchronously()
    {
        var (batch, _, _, _) = await SeedThreePeopleAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        Assert.Throws<InvalidOperationException>(() => context.People
            .Where(person => person.Name.StartsWith(batch))
            .SingleOrDefault());
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-SINGLE-OR-DEFAULT-MULTIPLE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleOrDefaultAsync")]
    public async Task SingleOrDefaultAsync_ThrowsWhenMultipleRowsMatch()
    {
        var (batch, _, _, _) = await SeedThreePeopleAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.People
            .Where(person => person.Name.StartsWith(batch))
            .SingleOrDefaultAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-LAST-SYNC", "System.Linq.Queryable.Last")]
    public async Task Last_ReturnsLastEntityInExplicitOrderSynchronously()
    {
        var (batch, _, _, expected) = await SeedThreePeopleAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        var actual = context.People.Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Age)
            .Last();

        Assert.Equal(expected.Id, actual.Id);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-LAST-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.LastAsync")]
    public async Task LastAsync_ReturnsLastEntityInExplicitOrder()
    {
        var (batch, _, _, expected) = await SeedThreePeopleAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        var actual = await context.People.Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Age)
            .LastAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected.Id, actual.Id);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-LAST-OR-DEFAULT-SYNC", "System.Linq.Queryable.LastOrDefault")]
    public async Task LastOrDefault_ReturnsDefaultForEmptyResultSynchronously()
    {
        var missingName = Guid.NewGuid().ToString("N");
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.Null(context.People.Where(person => person.Name == missingName).OrderBy(person => person.Age).LastOrDefault());
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-LAST-OR-DEFAULT-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.LastOrDefaultAsync")]
    public async Task LastOrDefaultAsync_ReturnsDefaultForEmptyResult()
    {
        var missingName = Guid.NewGuid().ToString("N");
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.Null(await context.People.Where(person => person.Name == missingName)
            .OrderBy(person => person.Age)
            .LastOrDefaultAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-ELEMENTAT-SYNC", "System.Linq.Queryable.ElementAt")]
    public async Task ElementAt_ReturnsEntityAtIndexSynchronously()
    {
        var (batch, _, expected, _) = await SeedThreePeopleAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        var actual = context.People.Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Age)
            .ElementAt(1);

        Assert.Equal(expected.Id, actual.Id);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-ELEMENTAT-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ElementAtAsync")]
    public async Task ElementAtAsync_ReturnsEntityAtIndex()
    {
        var (batch, _, expected, _) = await SeedThreePeopleAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        var actual = await context.People.Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Age)
            .ElementAtAsync(1, TestContext.Current.CancellationToken);

        Assert.Equal(expected.Id, actual.Id);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-ELEMENTAT-NEGATIVE-SYNC", "System.Linq.Queryable.ElementAt")]
    public async Task ElementAt_ThrowsForNegativeIndexSynchronously()
    {
        var (batch, _, _, _) = await SeedThreePeopleAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        Assert.Throws<ArgumentOutOfRangeException>(() => context.People
            .Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Age)
            .ElementAt(-1));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-ELEMENTAT-NEGATIVE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ElementAtAsync")]
    public async Task ElementAtAsync_ThrowsForNegativeIndex()
    {
        var (batch, _, _, _) = await SeedThreePeopleAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => context.People
            .Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Age)
            .ElementAtAsync(-1, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-ELEMENTAT-OR-DEFAULT-SYNC", "System.Linq.Queryable.ElementAtOrDefault")]
    public async Task ElementAtOrDefault_ReturnsDefaultOutOfRangeSynchronously()
    {
        var (batch, _, _, _) = await SeedThreePeopleAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        var actual = context.People.Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Age)
            .ElementAtOrDefault(10);

        Assert.Null(actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-ELEMENTAT-OR-DEFAULT-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ElementAtOrDefaultAsync")]
    public async Task ElementAtOrDefaultAsync_ReturnsDefaultOutOfRange()
    {
        var (batch, _, _, _) = await SeedThreePeopleAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        var actual = await context.People.Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Age)
            .ElementAtOrDefaultAsync(10, TestContext.Current.CancellationToken);

        Assert.Null(actual);
    }

    private static async Task<(string Batch, Person First, Person Middle, Person Last)> SeedThreePeopleAsync()
    {
        var batch = Guid.NewGuid().ToString("N");
        var people = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-first", Age = 18 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-middle", Age = 30 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-last", Age = 50 }
        };

        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.AddRange(people);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (batch, people[0], people[1], people[2]);
    }
}
