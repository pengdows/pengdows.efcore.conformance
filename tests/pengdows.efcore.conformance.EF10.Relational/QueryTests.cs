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
    [BehavioralCoverage("EF10-QUERY-DESCENDING-PAGING-SYNC", "System.Linq.Queryable.OrderByDescending", "System.Linq.Queryable.ThenByDescending", "System.Linq.Queryable.Skip", "System.Linq.Queryable.Take", "System.Linq.Queryable.ToArray")]
    public async Task DescendingOrdering_UsesTieBreakersAndPaginationSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Ada", Age = 30 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Grace", Age = 30 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Lin", Age = 20 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Mary", Age = 40 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = query.People
            .Where(person => person.Name.StartsWith(batch))
            .OrderByDescending(person => person.Age)
            .ThenByDescending(person => person.Name)
            .Skip(1)
            .Take(2)
            .Select(person => new PersonResult(person.Name, person.Age))
            .ToArray();

        Assert.Equal(new[] { new PersonResult($"{batch}-Grace", 30), new PersonResult($"{batch}-Ada", 30) }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-DESCENDING-PAGING-ASYNC", "System.Linq.Queryable.OrderByDescending", "System.Linq.Queryable.ThenByDescending", "System.Linq.Queryable.Skip", "System.Linq.Queryable.Take", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync")]
    public async Task DescendingOrdering_UsesTieBreakersAndPaginationAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Ada", Age = 30 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Grace", Age = 30 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Lin", Age = 20 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Mary", Age = 40 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = await query.People
            .Where(person => person.Name.StartsWith(batch))
            .OrderByDescending(person => person.Age)
            .ThenByDescending(person => person.Name)
            .Skip(1)
            .Take(2)
            .Select(person => new PersonResult(person.Name, person.Age))
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { new PersonResult($"{batch}-Grace", 30), new PersonResult($"{batch}-Ada", 30) }, actual);
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
    [BehavioralCoverage("EF10-QUERY-GROUPJOIN-LEFT-ASYNC", "System.Linq.Queryable.GroupJoin", "System.Linq.Queryable.SelectMany", "System.Linq.Queryable.DefaultIfEmpty", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task GroupJoinWithDefaultIfEmpty_ReturnsMatchesAndUnmatchedRowsAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-One", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Two", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Three", Age = 3 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var left = query.People.Where(person => person.Name.StartsWith(batch));
        var right = query.People.Where(person => person.Name.StartsWith(batch) && person.Age >= 2);
        var actual = await left.GroupJoin(
                right,
                outer => outer.Age + 1,
                inner => inner.Age,
                (outer, matches) => new { Outer = outer, Matches = matches })
            .SelectMany(
                joined => joined.Matches.DefaultIfEmpty(),
                (joined, inner) => new LeftJoinResult(joined.Outer.Age, inner == null ? null : inner.Name))
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
    [BehavioralCoverage("EF10-QUERY-GROUPJOIN-LEFT-SYNC", "System.Linq.Queryable.GroupJoin", "System.Linq.Queryable.SelectMany", "System.Linq.Queryable.DefaultIfEmpty", "System.Linq.Enumerable.ToList")]
    public async Task GroupJoinWithDefaultIfEmpty_ReturnsMatchesAndUnmatchedRowsSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-One", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Two", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Three", Age = 3 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var left = query.People.Where(person => person.Name.StartsWith(batch));
        var right = query.People.Where(person => person.Name.StartsWith(batch) && person.Age >= 2);
        var actual = left.GroupJoin(
                right,
                outer => outer.Age + 1,
                inner => inner.Age,
                (outer, matches) => new { Outer = outer, Matches = matches })
            .SelectMany(
                joined => joined.Matches.DefaultIfEmpty(),
                (joined, inner) => new LeftJoinResult(joined.Outer.Age, inner == null ? null : inner.Name))
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
    [BehavioralCoverage("EF10-LINQ-RIGHTJOIN-ASYNC", "System.Linq.Queryable.RightJoin", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task RightJoin_ReturnsMatchingAndMissingRightRows()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-One", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Two", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Three", Age = 3 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = await query.People
            .Where(person => person.Name.StartsWith(batch) && person.Age <= 2)
            .RightJoin(
                query.People.Where(person => person.Name.StartsWith(batch) && person.Age >= 2),
                outer => outer.Age,
                inner => inner.Age,
                (outer, inner) => new RightJoinResult(outer == null ? null : outer.Age, inner.Age))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new[] { new RightJoinResult(2, 2), new RightJoinResult(null, 3) },
            actual.OrderBy(result => result.RightAge).ToArray());
    }

    [Fact]
    [BehavioralCoverage("EF10-LINQ-RIGHTJOIN-SYNC", "System.Linq.Queryable.RightJoin", "System.Linq.Enumerable.ToList")]
    public async Task RightJoin_ReturnsMatchingAndMissingRightRowsSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-One", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Two", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Three", Age = 3 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = query.People
            .Where(person => person.Name.StartsWith(batch) && person.Age <= 2)
            .RightJoin(
                query.People.Where(person => person.Name.StartsWith(batch) && person.Age >= 2),
                outer => outer.Age,
                inner => inner.Age,
                (outer, inner) => new RightJoinResult(outer == null ? null : outer.Age, inner.Age))
            .ToList();

        Assert.Equal(
            new[] { new RightJoinResult(2, 2), new RightJoinResult(null, 3) },
            actual.OrderBy(result => result.RightAge).ToArray());
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-JOIN-SYNC", "System.Linq.Queryable.Join", "System.Linq.Queryable.ToArray")]
    public async Task Join_ReturnsOnlyRowsWithMatchingKeysSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-One", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Two", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Three", Age = 3 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var left = query.People.Where(person => person.Name.StartsWith(batch) && person.Age <= 2);
        var right = query.People.Where(person => person.Name.StartsWith(batch) && person.Age >= 2);
        var actual = left.Join(right, person => person.Age, person => person.Age,
                (outer, inner) => new JoinedAges(outer.Age, inner.Age))
            .ToArray();

        Assert.Equal(new[] { new JoinedAges(2, 2) }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-JOIN-ASYNC", "System.Linq.Queryable.Join", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync")]
    public async Task Join_ReturnsOnlyRowsWithMatchingKeysAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-One", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Two", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Three", Age = 3 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var left = query.People.Where(person => person.Name.StartsWith(batch) && person.Age <= 2);
        var right = query.People.Where(person => person.Name.StartsWith(batch) && person.Age >= 2);
        var actual = await left.Join(right, person => person.Age, person => person.Age,
                (outer, inner) => new JoinedAges(outer.Age, inner.Age))
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { new JoinedAges(2, 2) }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-SELECT-MANY-SYNC", "System.Linq.Queryable.SelectMany", "System.Linq.Queryable.ToArray")]
    public async Task SelectMany_ProjectsEveryPairFromTheComposedSequencesSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-One", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Two", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Three", Age = 3 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var outer = query.People.Where(person => person.Name.StartsWith(batch) && person.Age <= 2);
        var inner = query.People.Where(person => person.Name.StartsWith(batch) && person.Age >= 2);
        var actual = outer.SelectMany(
                _ => inner,
                (left, right) => new JoinedAges(left.Age, right.Age))
            .ToArray();

        Assert.Equal(new[] { new JoinedAges(1, 2), new JoinedAges(1, 3), new JoinedAges(2, 2), new JoinedAges(2, 3) },
            actual.OrderBy(result => result.LeftAge).ThenBy(result => result.RightAge).ToArray());
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-SELECT-MANY-ASYNC", "System.Linq.Queryable.SelectMany", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync")]
    public async Task SelectMany_ProjectsEveryPairFromTheComposedSequencesAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-One", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Two", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-Three", Age = 3 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var outer = query.People.Where(person => person.Name.StartsWith(batch) && person.Age <= 2);
        var inner = query.People.Where(person => person.Name.StartsWith(batch) && person.Age >= 2);
        var actual = await outer.SelectMany(
                _ => inner,
                (left, right) => new JoinedAges(left.Age, right.Age))
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { new JoinedAges(1, 2), new JoinedAges(1, 3), new JoinedAges(2, 2), new JoinedAges(2, 3) },
            actual.OrderBy(result => result.LeftAge).ThenBy(result => result.RightAge).ToArray());
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-STRING-CONTAINS-SYNC", "System.String.Contains(System.String)", "System.Linq.Queryable.Where", "System.Linq.Queryable.ToArray")]
    public async Task StringContains_FiltersByParameterizedSubstringSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-target-suffix", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-target-other", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-other-suffix", Age = 3 }
        });

        var substring = "target";
        await using var query = ProviderTestRuntime.CreateContext();
        var names = query.People
            .Where(person => person.Name.StartsWith(batch) && person.Name.Contains(substring))
            .Select(person => person.Name)
            .ToArray();

        Assert.Equal(new[] { $"{batch}-target-other", $"{batch}-target-suffix" }, names.Order(StringComparer.Ordinal));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-STRING-CONTAINS-ASYNC", "System.String.Contains(System.String)", "System.Linq.Queryable.Where", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync")]
    public async Task StringContains_FiltersByParameterizedSubstringAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-target-suffix", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-target-other", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-other-suffix", Age = 3 }
        });

        var substring = "target";
        await using var query = ProviderTestRuntime.CreateContext();
        var names = await query.People
            .Where(person => person.Name.StartsWith(batch) && person.Name.Contains(substring))
            .Select(person => person.Name)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { $"{batch}-target-other", $"{batch}-target-suffix" }, names.Order(StringComparer.Ordinal));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-STRING-ENDS-WITH-SYNC", "System.String.EndsWith(System.String)", "System.Linq.Queryable.Where", "System.Linq.Queryable.ToArray")]
    public async Task StringEndsWith_FiltersByParameterizedSuffixSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-target-suffix", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-target-other", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-other-suffix", Age = 3 }
        });

        var suffix = "suffix";
        await using var query = ProviderTestRuntime.CreateContext();
        var names = query.People
            .Where(person => person.Name.StartsWith(batch) && person.Name.EndsWith(suffix))
            .Select(person => person.Name)
            .ToArray();

        Assert.Equal(new[] { $"{batch}-other-suffix", $"{batch}-target-suffix" }, names.Order(StringComparer.Ordinal));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-STRING-ENDS-WITH-ASYNC", "System.String.EndsWith(System.String)", "System.Linq.Queryable.Where", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync")]
    public async Task StringEndsWith_FiltersByParameterizedSuffixAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-target-suffix", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-target-other", Age = 2 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-other-suffix", Age = 3 }
        });

        var suffix = "suffix";
        await using var query = ProviderTestRuntime.CreateContext();
        var names = await query.People
            .Where(person => person.Name.StartsWith(batch) && person.Name.EndsWith(suffix))
            .Select(person => person.Name)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { $"{batch}-other-suffix", $"{batch}-target-suffix" }, names.Order(StringComparer.Ordinal));
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
    [BehavioralCoverage("EF10-QUERY-ASYNC-MATERIALIZERS", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToHashSetAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToDictionaryAsync")]
    public async Task AsyncMaterializers_ReturnExpectedArraySetAndDictionary()
    {
        var people = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = "Materialize Ada", Age = 36 },
            new Person { Id = Guid.NewGuid(), Name = "Materialize Grace", Age = 85 },
            new Person { Id = Guid.NewGuid(), Name = "Materialize Katherine", Age = 36 }
        };
        await SeedAsync(people);

        await using var context = ProviderTestRuntime.CreateContext();
        var query = context.People.Where(person => person.Name.StartsWith("Materialize"));

        var array = await query.OrderBy(person => person.Name)
            .Select(person => person.Name)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        var idSet = await query.Select(person => person.Id)
            .ToHashSetAsync(TestContext.Current.CancellationToken);
        var byId = await query.ToDictionaryAsync(
            person => person.Id,
            person => person.Name,
            TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Materialize Ada", "Materialize Grace", "Materialize Katherine" }, array);
        Assert.True(idSet.SetEquals(people.Select(person => person.Id)));
        Assert.Equal(people.Length, byId.Count);
        Assert.All(people, person => Assert.Equal(person.Name, byId[person.Id]));
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
    [BehavioralCoverage("EF10-QUERY-FOR-EACH-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ForEachAsync")]
    public async Task ForEachAsync_VisitsEveryMatchingEntity()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(Enumerable.Range(0, 4).Select(index => new Person
        {
            Id = Guid.NewGuid(),
            Name = $"{batch}-{index}",
            Age = index
        }));

        await using var context = ProviderTestRuntime.CreateContext();
        var names = new List<string>();
        await context.People
            .Where(person => person.Name.StartsWith(batch))
            .ForEachAsync(person => names.Add(person.Name), TestContext.Current.CancellationToken);

        Assert.Equal(Enumerable.Range(0, 4).Select(index => $"{batch}-{index}"), names.Order(StringComparer.Ordinal));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-LOAD-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.LoadAsync", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.Entries")]
    public async Task LoadAsync_TracksEveryMatchingEntity()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(Enumerable.Range(0, 3).Select(index => new Person
        {
            Id = Guid.NewGuid(),
            Name = $"{batch}-{index}",
            Age = index
        }));

        await using var context = ProviderTestRuntime.CreateContext();
        await context.People
            .Where(person => person.Name.StartsWith(batch))
            .LoadAsync(TestContext.Current.CancellationToken);

        var tracked = context.ChangeTracker.Entries<Person>()
            .Select(entry => entry.Entity.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(Enumerable.Range(0, 3).Select(index => $"{batch}-{index}").Order(StringComparer.Ordinal), tracked);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-LOAD-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Load", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.Entries")]
    public async Task Load_TracksEveryMatchingEntitySynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(Enumerable.Range(0, 3).Select(index => new Person
        {
            Id = Guid.NewGuid(),
            Name = $"{batch}-{index}",
            Age = index
        }));

        await using var context = ProviderTestRuntime.CreateContext();
        context.People
            .Where(person => person.Name.StartsWith(batch))
            .Load();

        var tracked = context.ChangeTracker.Entries<Person>()
            .Select(entry => entry.Entity.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(Enumerable.Range(0, 3).Select(index => $"{batch}-{index}").Order(StringComparer.Ordinal), tracked);
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

    [Fact]
    [BehavioralCoverage("EF10-QUERY-NULL-PREDICATE-ASYNC", "System.Linq.Queryable.Where", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task NullablePredicate_FiltersNullAndNonNullValuesAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-null", Age = 20, OptionalRank = null },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-value", Age = 21, OptionalRank = 4 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var nullMatches = await query.People
            .Where(person => person.Name.StartsWith(batch) && person.OptionalRank == null)
            .Select(person => person.Name)
            .ToListAsync(TestContext.Current.CancellationToken);
        var valueMatches = await query.People
            .Where(person => person.Name.StartsWith(batch) && person.OptionalRank != null)
            .Select(person => person.OptionalRank)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { $"{batch}-null" }, nullMatches);
        Assert.Equal(new int?[] { 4 }, valueMatches);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-NULL-PREDICATE-SYNC", "System.Linq.Queryable.Where", "System.Linq.Queryable.ToList")]
    public async Task NullablePredicate_FiltersNullAndNonNullValuesSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-null", Age = 20, OptionalRank = null },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-value", Age = 21, OptionalRank = 4 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var nullMatches = query.People
            .Where(person => person.Name.StartsWith(batch) && person.OptionalRank == null)
            .Select(person => person.Name)
            .ToList();
        var valueMatches = query.People
            .Where(person => person.Name.StartsWith(batch) && person.OptionalRank != null)
            .Select(person => person.OptionalRank)
            .ToList();

        Assert.Equal(new[] { $"{batch}-null" }, nullMatches);
        Assert.Equal(new int?[] { 4 }, valueMatches);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-NULL-COMPENSATION-SYNC", "System.Linq.Queryable.Where", "System.Linq.Queryable.Select", "System.Linq.Queryable.ToArray")]
    public async Task NullableColumnComparison_AppliesNullCompensationSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-null", Age = 20, OptionalRank = null },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-equal", Age = 20, OptionalRank = 20 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-different", Age = 20, OptionalRank = 21 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = query.People
            .Where(person => person.Name.StartsWith(batch) && person.OptionalRank != person.Age)
            .Select(person => person.Name)
            .ToArray();

        Assert.Equal(new[] { $"{batch}-different", $"{batch}-null" }.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-NULL-COMPENSATION-ASYNC", "System.Linq.Queryable.Where", "System.Linq.Queryable.Select", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync")]
    public async Task NullableColumnComparison_AppliesNullCompensationAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-null", Age = 20, OptionalRank = null },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-equal", Age = 20, OptionalRank = 20 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-different", Age = 20, OptionalRank = 21 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = await query.People
            .Where(person => person.Name.StartsWith(batch) && person.OptionalRank != person.Age)
            .Select(person => person.Name)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { $"{batch}-different", $"{batch}-null" }.Order(StringComparer.Ordinal), actual.Order(StringComparer.Ordinal));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-CANCELLATION", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task ToListAsync_ObservesCancellationBeforeQueryExecution()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await context.People.ToListAsync(cancellation.Token));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-SHADOW-PROPERTY-ASYNC", "Microsoft.EntityFrameworkCore.EF.Property", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.Property", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task EfProperty_FiltersOnShadowPropertyAsynchronously()
    {
        var id = Guid.NewGuid();
        var label = $"label-{Guid.NewGuid():N}";
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            var person = new Person { Id = id, Name = "Shadow property query", Age = 34 };
            write.People.Add(person);
            write.Entry(person).Property<string>("ShadowLabel").CurrentValue = label;
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var query = ProviderTestRuntime.CreateContext();
        var names = await query.People
            .Where(person => EF.Property<string>(person, "ShadowLabel") == label)
            .Select(person => person.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Shadow property query" }, names);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-SHADOW-PROPERTY-SYNC", "Microsoft.EntityFrameworkCore.EF.Property", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.Property", "System.Linq.Queryable.ToList")]
    public async Task EfProperty_FiltersOnShadowPropertySynchronously()
    {
        var id = Guid.NewGuid();
        var label = $"label-{Guid.NewGuid():N}";
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            var person = new Person { Id = id, Name = "Shadow property query sync", Age = 35 };
            write.People.Add(person);
            write.Entry(person).Property<string>("ShadowLabel").CurrentValue = label;
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var query = ProviderTestRuntime.CreateContext();
        var names = query.People
            .Where(person => EF.Property<string>(person, "ShadowLabel") == label)
            .Select(person => person.Name)
            .ToList();

        Assert.Equal(new[] { "Shadow property query sync" }, names);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-EF-CONSTANT-PARAMETER-ASYNC", "Microsoft.EntityFrameworkCore.EF.Constant", "Microsoft.EntityFrameworkCore.EF.Parameter", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task EfConstantAndParameter_PreservePredicateResultsAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-young", Age = 19 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-middle", Age = 25 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-older", Age = 40 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var minimumAge = 25;
        var constantNames = await query.People
            .Where(person => person.Name.StartsWith(batch) && person.Age >= EF.Constant(minimumAge))
            .Select(person => person.Name)
            .ToListAsync(TestContext.Current.CancellationToken);
        var parameterNames = await query.People
            .Where(person => person.Name.StartsWith(batch) && person.Age >= EF.Parameter(minimumAge))
            .Select(person => person.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        var expected = new[] { $"{batch}-middle", $"{batch}-older" };
        Assert.Equal(expected, constantNames.Order(StringComparer.Ordinal));
        Assert.Equal(expected, parameterNames.Order(StringComparer.Ordinal));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-EF-CONSTANT-PARAMETER-SYNC", "Microsoft.EntityFrameworkCore.EF.Constant", "Microsoft.EntityFrameworkCore.EF.Parameter", "System.Linq.Queryable.ToList")]
    public async Task EfConstantAndParameter_PreservePredicateResultsSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-young", Age = 19 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-middle", Age = 25 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-older", Age = 40 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var minimumAge = 25;
        var constantNames = query.People
            .Where(person => person.Name.StartsWith(batch) && person.Age >= EF.Constant(minimumAge))
            .Select(person => person.Name)
            .ToList();
        var parameterNames = query.People
            .Where(person => person.Name.StartsWith(batch) && person.Age >= EF.Parameter(minimumAge))
            .Select(person => person.Name)
            .ToList();

        var expected = new[] { $"{batch}-middle", $"{batch}-older" };
        Assert.Equal(expected, constantNames.Order(StringComparer.Ordinal));
        Assert.Equal(expected, parameterNames.Order(StringComparer.Ordinal));
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-LIKE-ASYNC", "Microsoft.EntityFrameworkCore.DbFunctionsExtensions.Like", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task Like_EscapesPatternWildcardsWhenRequestedAsynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        var matchingName = $"{batch}_literal";
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = matchingName, Age = 30 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}Xliteral", Age = 31 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var names = await query.People
            .Where(person => EF.Functions.Like(person.Name, $"{batch}!_%", "!"))
            .Select(person => person.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { matchingName }, names);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-LIKE-SYNC", "Microsoft.EntityFrameworkCore.DbFunctionsExtensions.Like", "System.Linq.Queryable.ToList")]
    public async Task Like_EscapesPatternWildcardsWhenRequestedSynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        var matchingName = $"{batch}_literal";
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = matchingName, Age = 30 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}Xliteral", Age = 31 }
        });

        await using var query = ProviderTestRuntime.CreateContext();
        var names = query.People
            .Where(person => EF.Functions.Like(person.Name, $"{batch}!_%", "!"))
            .Select(person => person.Name)
            .ToList();

        Assert.Equal(new[] { matchingName }, names);
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

    private sealed record RightJoinResult(int? LeftAge, int RightAge);

    private sealed record JoinedAges(int LeftAge, int RightAge);

}
