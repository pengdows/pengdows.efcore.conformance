using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class QueryTests
{
    [Fact]
    [BehavioralCoverage("EF10-QUERY-COMPOSITION-ASYNC", "System.Linq.Queryable.Where", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.ThenBy", "System.Linq.Queryable.Skip", "System.Linq.Queryable.Take", "System.Linq.Queryable.Select", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task ComposedQuery_FiltersOrdersPaginatesAndProjects()
    {
        var batch = Guid.NewGuid().ToString("N");
        var people = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Ada", Age = 36 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Grace", Age = 85 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Katherine", Age = 36 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Mary", Age = 36 }
        };

        await SeedAsync(people);

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = await query.People
            .Where(person => person.Name.StartsWith(batch) && person.Age >= 36)
            .OrderBy(person => person.Age)
            .ThenBy(person => person.Name)
            .Skip(1)
            .Take(2)
            .Select(person => new PersonResult(person.Name, person.Age))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { new PersonResult($"{batch}-Katherine", 36), new PersonResult($"{batch}-Mary", 36) },
            actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-COMPOSITION-SYNC", "System.Linq.Queryable.Where", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.ThenBy", "System.Linq.Queryable.Skip", "System.Linq.Queryable.Take", "System.Linq.Queryable.Select", "System.Linq.Enumerable.ToList")]
    public async Task ComposedQuery_FiltersOrdersPaginatesAndProjectsSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        var people = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Ada", Age = 36 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Grace", Age = 85 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Katherine", Age = 36 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Mary", Age = 36 }
        };

        await SeedAsync(people);

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = query.People
            .Where(person => person.Name.StartsWith(batch) && person.Age >= 36)
            .OrderBy(person => person.Age)
            .ThenBy(person => person.Name)
            .Skip(1)
            .Take(2)
            .Select(person => new PersonResult(person.Name, person.Age))
            .ToList();

        Assert.Equal(
            new[] { new PersonResult($"{batch}-Katherine", 36), new PersonResult($"{batch}-Mary", 36) },
            actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-LINQ-LEFTJOIN-ASYNC", "System.Linq.Queryable.LeftJoin", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task LeftJoin_ReturnsMatchingAndMissingRows()
    {
        var batch = Guid.NewGuid().ToString("N");
        var people = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-One", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Two", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Three", Age = 3 }
        };
        await SeedAsync(people);

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = await query.People.Where(person => person.Name.StartsWith(batch))
            .LeftJoin(
                query.People.Where(person => person.Name.StartsWith(batch)),
                outer => outer.Age + 1,
                inner => inner.Age,
                (outer, inner) => new LeftJoinResult(outer.Age, inner == null ? null : inner.Name))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new[]
            {
                new LeftJoinResult(1, $"{batch}-Two"),
                new LeftJoinResult(2, $"{batch}-Three"),
                new LeftJoinResult(3, null)
            },
            actual.OrderBy(result => result.Age).ToArray());
    }

    [Fact]
    [BehavioralCoverage("EF10-LINQ-LEFTJOIN-SYNC", "System.Linq.Queryable.LeftJoin", "System.Linq.Enumerable.ToList")]
    public async Task LeftJoin_ReturnsMatchingAndMissingRowsSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        var people = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-One", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Two", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Three", Age = 3 }
        };
        await SeedAsync(people);

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = query.People.Where(person => person.Name.StartsWith(batch))
            .LeftJoin(
                query.People.Where(person => person.Name.StartsWith(batch)),
                outer => outer.Age + 1,
                inner => inner.Age,
                (outer, inner) => new LeftJoinResult(outer.Age, inner == null ? null : inner.Name))
            .ToList();

        Assert.Equal(
            new[]
            {
                new LeftJoinResult(1, $"{batch}-Two"),
                new LeftJoinResult(2, $"{batch}-Three"),
                new LeftJoinResult(3, null)
            },
            actual.OrderBy(result => result.Age).ToArray());
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-GROUPBY-ASYNC", "System.Linq.Queryable.GroupBy", "System.Linq.Queryable.Count", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task GroupByAndAggregate_ReturnsExpectedGroupsAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(Enumerable.Range(0, 5).Select(index => new Person
        {
            Id = Guid.NewGuid(),
            Name = $"{batch}-{index}",
            Age = index < 2 ? 20 : 30
        }));

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = await query.People
            .Where(person => person.Name.StartsWith(batch))
            .GroupBy(person => person.Age)
            .Select(group => new { Age = group.Key, Count = group.Count() })
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { new { Age = 20, Count = 2 }, new { Age = 30, Count = 3 } },
            actual.OrderBy(group => group.Age).ToArray());
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-GROUPBY-SYNC", "System.Linq.Queryable.GroupBy", "System.Linq.Queryable.Count", "System.Linq.Queryable.ToList")]
    public async Task GroupByAndAggregate_ReturnsExpectedGroupsSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(Enumerable.Range(0, 5).Select(index => new Person
        {
            Id = Guid.NewGuid(),
            Name = $"{batch}-{index}",
            Age = index < 2 ? 20 : 30
        }));

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = query.People
            .Where(person => person.Name.StartsWith(batch))
            .GroupBy(person => person.Age)
            .Select(group => new { Age = group.Key, Count = group.Count() })
            .ToList();

        Assert.Equal(
            new[] { new { Age = 20, Count = 2 }, new { Age = 30, Count = 3 } },
            actual.OrderBy(group => group.Age).ToArray());
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-AGGREGATES-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SumAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AverageAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.MinAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.MaxAsync")]
    public async Task Aggregates_ReturnExpectedValuesAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(Enumerable.Range(0, 5).Select(index => new Person
        {
            Id = Guid.NewGuid(),
            Name = $"{batch}-{index}",
            Age = index < 2 ? 20 : 30
        }));

        await using var query = ProviderTestRuntime.CreateContext();
        var people = query.People.Where(person => person.Name.StartsWith(batch));
        Assert.Equal(5, await people.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(130, await people.SumAsync(person => person.Age, TestContext.Current.CancellationToken));
        Assert.Equal(26, await people.AverageAsync(person => person.Age, TestContext.Current.CancellationToken));
        Assert.Equal(20, await people.MinAsync(person => person.Age, TestContext.Current.CancellationToken));
        Assert.Equal(30, await people.MaxAsync(person => person.Age, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-AGGREGATES-SYNC", "System.Linq.Queryable.Count", "System.Linq.Queryable.Sum", "System.Linq.Queryable.Average", "System.Linq.Queryable.Min", "System.Linq.Queryable.Max")]
    public async Task Aggregates_ReturnExpectedValuesSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(Enumerable.Range(0, 5).Select(index => new Person
        {
            Id = Guid.NewGuid(),
            Name = $"{batch}-{index}",
            Age = index < 2 ? 20 : 30
        }));

        await using var query = ProviderTestRuntime.CreateContext();
        var people = query.People.Where(person => person.Name.StartsWith(batch));
        Assert.Equal(5, people.Count());
        Assert.Equal(130, people.Sum(person => person.Age));
        Assert.Equal(26, people.Average(person => person.Age));
        Assert.Equal(20, people.Min(person => person.Age));
        Assert.Equal(30, people.Max(person => person.Age));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-STREAM-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsAsyncEnumerable")]
    public async Task AsAsyncEnumerable_StreamsMatchingEntities()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(Enumerable.Range(0, 3).Select(index => new Person
        {
            Id = Guid.NewGuid(),
            Name = $"{batch}-{index}",
            Age = index
        }));

        await using var query = ProviderTestRuntime.CreateContext();
        var names = new List<string>();
        await foreach (var person in query.People.Where(person => person.Name.StartsWith(batch))
                           .AsAsyncEnumerable()
                           .WithCancellation(TestContext.Current.CancellationToken))
        {
            names.Add(person.Name);
        }

        Assert.Equal(new[] { $"{batch}-0", $"{batch}-1", $"{batch}-2" }, names.Order(StringComparer.Ordinal));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-STREAM-SYNC", "System.Linq.IQueryable.GetEnumerator")]
    public async Task QueryableEnumeration_StreamsMatchingEntitiesSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(Enumerable.Range(0, 3).Select(index => new Person
        {
            Id = Guid.NewGuid(),
            Name = $"{batch}-{index}",
            Age = index
        }));

        await using var query = ProviderTestRuntime.CreateContext();
        var names = query.People.Where(person => person.Name.StartsWith(batch)).Select(person => person.Name).ToList();
        Assert.Equal(new[] { $"{batch}-0", $"{batch}-1", $"{batch}-2" }, names.Order(StringComparer.Ordinal));
    }

    private static async Task SeedAsync(IEnumerable<Person> people)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.AddRange(people);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed record PersonResult(string Name, int Age);

    private sealed record LeftJoinResult(int Age, string? MatchName);

}
