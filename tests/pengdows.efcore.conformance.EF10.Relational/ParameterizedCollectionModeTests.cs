using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class ParameterizedCollectionModeTests
{
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
