using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class ParameterizedCollectionModeTests
{
    [Fact]
    [BehavioralCoverage("EF10-QUERY-COLLECTION-EDGE-MODES-SYNC", "Microsoft.EntityFrameworkCore.ParameterTranslationMode.MultipleParameters", "Microsoft.EntityFrameworkCore.ParameterTranslationMode.Parameter", "Microsoft.EntityFrameworkCore.ParameterTranslationMode.Constant", "Microsoft.EntityFrameworkCore.Infrastructure.RelationalDbContextOptionsBuilder`2.UseParameterizedCollectionMode", "System.Linq.Queryable.Where", "System.Linq.Queryable.ToArray")]
    public async Task EmptySingletonAndNullContainingCollections_ReturnEquivalentRowsSynchronously()
    {
        ProviderTestRuntime.RequireCapability("parameterized-collection-modes");
        var batch = Guid.NewGuid().ToString("N");
        var seeded = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-null", Age = 20, OptionalLabel = null },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-empty", Age = 21, OptionalLabel = string.Empty },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-needle", Age = 22, OptionalLabel = "needle" },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-other", Age = 23, OptionalLabel = "other" }
        };
        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.People.AddRange(seeded);
            Assert.Equal(4, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        string[]? baseline = null;
        foreach (var mode in CollectionModes)
        {
            await using var context = ProviderTestRuntime.CreateContext(mode);
            var people = context.People.Where(person => person.Name.StartsWith(batch));
            var empty = people.Where(person => Array.Empty<string?>().Contains(person.OptionalLabel)).Select(person => person.Name).ToArray();
            var singleton = people.Where(person => new string?[] { "needle" }.Contains(person.OptionalLabel)).Select(person => person.Name).ToArray();
            var withNull = people.Where(person => new string?[] { null, "needle" }.Contains(person.OptionalLabel)).Select(person => person.Name).ToArray();

            Assert.Empty(empty);
            Assert.Equal(new[] { $"{batch}-needle" }, singleton);
            Assert.Equal(new[] { $"{batch}-needle", $"{batch}-null" }.Order(StringComparer.Ordinal), withNull.Order(StringComparer.Ordinal));
            var current = empty.Concat(singleton).Concat(withNull).Order(StringComparer.Ordinal).ToArray();
            baseline ??= current;
            Assert.Equal(baseline, current);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-COLLECTION-EDGE-MODES-ASYNC", "Microsoft.EntityFrameworkCore.ParameterTranslationMode.MultipleParameters", "Microsoft.EntityFrameworkCore.ParameterTranslationMode.Parameter", "Microsoft.EntityFrameworkCore.ParameterTranslationMode.Constant", "Microsoft.EntityFrameworkCore.Infrastructure.RelationalDbContextOptionsBuilder`2.UseParameterizedCollectionMode", "System.Linq.Queryable.Where", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync")]
    public async Task EmptySingletonAndNullContainingCollections_ReturnEquivalentRowsAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("parameterized-collection-modes");
        var batch = Guid.NewGuid().ToString("N");
        var seeded = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-null", Age = 20, OptionalLabel = null },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-empty", Age = 21, OptionalLabel = string.Empty },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-needle", Age = 22, OptionalLabel = "needle" },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-other", Age = 23, OptionalLabel = "other" }
        };
        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.People.AddRange(seeded);
            Assert.Equal(4, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        string[]? baseline = null;
        foreach (var mode in CollectionModes)
        {
            await using var context = ProviderTestRuntime.CreateContext(mode);
            var empty = await context.People.Where(person => person.Name.StartsWith(batch) && Array.Empty<string?>().Contains(person.OptionalLabel))
                .Select(person => person.Name)
                .ToArrayAsync(TestContext.Current.CancellationToken);
            var singleton = await context.People.Where(person => person.Name.StartsWith(batch) && new string?[] { "needle" }.Contains(person.OptionalLabel))
                .Select(person => person.Name)
                .ToArrayAsync(TestContext.Current.CancellationToken);
            var withNull = await context.People.Where(person => person.Name.StartsWith(batch) && new string?[] { null, "needle" }.Contains(person.OptionalLabel))
                .Select(person => person.Name)
                .ToArrayAsync(TestContext.Current.CancellationToken);

            Assert.Empty(empty);
            Assert.Equal(new[] { $"{batch}-needle" }, singleton);
            Assert.Equal(new[] { $"{batch}-needle", $"{batch}-null" }.Order(StringComparer.Ordinal), withNull.Order(StringComparer.Ordinal));
            var current = empty.Concat(singleton).Concat(withNull).Order(StringComparer.Ordinal).ToArray();
            baseline ??= current;
            Assert.Equal(baseline, current);
        }
    }

    private static readonly ParameterTranslationMode[] CollectionModes =
    [
        ParameterTranslationMode.MultipleParameters,
        ParameterTranslationMode.Parameter,
        ParameterTranslationMode.Constant
    ];

    [Fact]
    [BehavioralCoverage("EF10-QUERY-MULTIPLE-PARAMETERS-METHOD-SYNC", "Microsoft.EntityFrameworkCore.EFExtensions.MultipleParameters", "Microsoft.EntityFrameworkCore.EFExtensions+<G>$E875929AFAAB9F9D41CDDE4E1FF47548.MultipleParameters", "Microsoft.EntityFrameworkCore.EFExtensions+<G>$E875929AFAAB9F9D41CDDE4E1FF47548+<M>$B9DED21D8FA4468F26592C2A680520F7.<Extension>$", "System.Linq.Queryable.Where", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.Select", "System.Linq.Queryable.ToArray")]
    public async Task MultipleParameters_TranslatesCapturedCollectionsAndReturnsExpectedRowsSynchronously()
    {
        var ids = await SeedAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        var actual = context.People
            .Where(person => EF.MultipleParameters(ids).Contains(person.Id))
            .OrderBy(person => person.Age)
            .Select(person => person.Name)
            .ToArray();

        Assert.Equal(new[] { "Collection young", "Collection old" }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-MULTIPLE-PARAMETERS-METHOD-ASYNC", "Microsoft.EntityFrameworkCore.EFExtensions.MultipleParameters", "Microsoft.EntityFrameworkCore.EFExtensions+<G>$E875929AFAAB9F9D41CDDE4E1FF47548.MultipleParameters", "Microsoft.EntityFrameworkCore.EFExtensions+<G>$E875929AFAAB9F9D41CDDE4E1FF47548+<M>$B9DED21D8FA4468F26592C2A680520F7.<Extension>$", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync", "System.Linq.Queryable.Where", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.Select")]
    public async Task MultipleParameters_TranslatesCapturedCollectionsAndReturnsExpectedRowsAsynchronously()
    {
        var ids = await SeedAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        var actual = await context.People
            .Where(person => EF.MultipleParameters(ids).Contains(person.Id))
            .OrderBy(person => person.Age)
            .Select(person => person.Name)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Collection young", "Collection old" }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-COLLECTION-MODE-MULTIPLE-PARAMETERS-SYNC", "Microsoft.EntityFrameworkCore.ParameterTranslationMode.MultipleParameters", "Microsoft.EntityFrameworkCore.Infrastructure.RelationalDbContextOptionsBuilder`2.UseParameterizedCollectionMode", "System.Linq.Queryable.Where", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.Select", "System.Linq.Queryable.ToArray")]
    public async Task TranslateParameterizedCollectionsToMultipleParameters_ReturnsMatchingRowsSynchronously()
    {
        var ids = await SeedAsync();
        await using var context = ProviderTestRuntime.CreateContext(ParameterTranslationMode.MultipleParameters);

        var actual = context.People
            .Where(person => ids.Contains(person.Id))
            .OrderBy(person => person.Age)
            .Select(person => person.Name)
            .ToArray();

        Assert.Equal(new[] { "Collection young", "Collection old" }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-COLLECTION-MODE-MULTIPLE-PARAMETERS-ASYNC", "Microsoft.EntityFrameworkCore.ParameterTranslationMode.MultipleParameters", "Microsoft.EntityFrameworkCore.Infrastructure.RelationalDbContextOptionsBuilder`2.UseParameterizedCollectionMode", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync", "System.Linq.Queryable.Where", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.Select")]
    public async Task TranslateParameterizedCollectionsToMultipleParameters_ReturnsMatchingRowsAsynchronously()
    {
        var ids = await SeedAsync();
        await using var context = ProviderTestRuntime.CreateContext(ParameterTranslationMode.MultipleParameters);

        var actual = await context.People
            .Where(person => ids.Contains(person.Id))
            .OrderBy(person => person.Age)
            .Select(person => person.Name)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Collection young", "Collection old" }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-COLLECTION-MODE-CONSTANTS-SYNC", "Microsoft.EntityFrameworkCore.ParameterTranslationMode", "Microsoft.EntityFrameworkCore.ParameterTranslationMode.Constant", "Microsoft.EntityFrameworkCore.Infrastructure.RelationalDbContextOptionsBuilder`2.UseParameterizedCollectionMode", "System.Linq.Queryable.Where", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.Select", "System.Linq.Queryable.ToArray")]
    public async Task TranslateParameterizedCollectionsToConstants_ReturnsMatchingRowsSynchronously()
    {
        var ids = await SeedAsync();
        await using var context = ProviderTestRuntime.CreateContext(ParameterTranslationMode.Constant);

        var actual = context.People
            .Where(person => ids.Contains(person.Id))
            .OrderBy(person => person.Age)
            .Select(person => person.Name)
            .ToArray();

        Assert.Equal(new[] { "Collection young", "Collection old" }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-COLLECTION-MODE-CONSTANTS-ASYNC", "Microsoft.EntityFrameworkCore.ParameterTranslationMode", "Microsoft.EntityFrameworkCore.ParameterTranslationMode.Constant", "Microsoft.EntityFrameworkCore.Infrastructure.RelationalDbContextOptionsBuilder`2.UseParameterizedCollectionMode", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync", "System.Linq.Queryable.Where", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.Select")]
    public async Task TranslateParameterizedCollectionsToConstants_ReturnsMatchingRowsAsynchronously()
    {
        var ids = await SeedAsync();
        await using var context = ProviderTestRuntime.CreateContext(ParameterTranslationMode.Constant);

        var actual = await context.People
            .Where(person => ids.Contains(person.Id))
            .OrderBy(person => person.Age)
            .Select(person => person.Name)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Collection young", "Collection old" }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-COLLECTION-MODE-PARAMETERS-SYNC", "Microsoft.EntityFrameworkCore.ParameterTranslationMode", "Microsoft.EntityFrameworkCore.ParameterTranslationMode.Parameter", "Microsoft.EntityFrameworkCore.Infrastructure.RelationalDbContextOptionsBuilder`2.UseParameterizedCollectionMode", "System.Linq.Queryable.Where", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.Select", "System.Linq.Queryable.ToArray")]
    public async Task TranslateParameterizedCollectionsToParameters_ReturnsMatchingRowsSynchronously()
    {
        var ids = await SeedAsync();
        await using var context = ProviderTestRuntime.CreateContext(ParameterTranslationMode.Parameter);

        var actual = context.People
            .Where(person => ids.Contains(person.Id))
            .OrderBy(person => person.Age)
            .Select(person => person.Name)
            .ToArray();

        Assert.Equal(new[] { "Collection young", "Collection old" }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-COLLECTION-MODE-PARAMETERS-ASYNC", "Microsoft.EntityFrameworkCore.ParameterTranslationMode", "Microsoft.EntityFrameworkCore.ParameterTranslationMode.Parameter", "Microsoft.EntityFrameworkCore.Infrastructure.RelationalDbContextOptionsBuilder`2.UseParameterizedCollectionMode", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync", "System.Linq.Queryable.Where", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.Select")]
    public async Task TranslateParameterizedCollectionsToParameters_ReturnsMatchingRowsAsynchronously()
    {
        var ids = await SeedAsync();
        await using var context = ProviderTestRuntime.CreateContext(ParameterTranslationMode.Parameter);

        var actual = await context.People
            .Where(person => ids.Contains(person.Id))
            .OrderBy(person => person.Age)
            .Select(person => person.Name)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Collection young", "Collection old" }, actual);
    }

    private static async Task<Guid[]> SeedAsync()
    {
        var youngId = Guid.NewGuid();
        var oldId = Guid.NewGuid();

        await using var seed = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
        seed.People.AddRange(
            new Person { Id = youngId, Name = "Collection young", Age = 21 },
            new Person { Id = oldId, Name = "Collection old", Age = 47 });
        Assert.Equal(2, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));

        return [oldId, youngId];
    }
}
