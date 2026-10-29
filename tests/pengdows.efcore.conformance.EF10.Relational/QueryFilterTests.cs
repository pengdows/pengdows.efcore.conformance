using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class QueryFilterTests
{
    [Fact]
    [BehavioralCoverage("EF10-QUERY-FILTERS-NAMED-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder`1.HasQueryFilter", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters", "System.Linq.Queryable.Where", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task NamedFilters_ApplyByDefaultAndCanBeDisabledIndividually()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var visibleByDefault = await context.People
            .Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Name)
            .Select(person => person.Name)
            .ToListAsync(TestContext.Current.CancellationToken);
        var withSoftDeleteDisabled = await context.People
            .IgnoreQueryFilters(["SoftDelete"])
            .Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Name)
            .Select(person => person.Name)
            .ToListAsync(TestContext.Current.CancellationToken);
        var withAllFiltersDisabled = await context.People
            .IgnoreQueryFilters()
            .Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Name)
            .Select(person => person.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { $"{batch}-active" }, visibleByDefault);
        Assert.Equal(new[] { $"{batch}-active", $"{batch}-deleted" }, withSoftDeleteDisabled);
        Assert.Equal(new[] { $"{batch}-active", $"{batch}-archived", $"{batch}-both", $"{batch}-deleted" }, withAllFiltersDisabled);
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-FILTERS-NAMED-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreQueryFilters", "System.Linq.Queryable.Where", "System.Linq.Enumerable.ToList")]
    public async Task NamedFilters_CanBeDisabledIndividuallySynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(batch);

        await using var context = ProviderTestRuntime.CreateContext();
        var withSoftDeleteDisabled = context.People
            .IgnoreQueryFilters(["SoftDelete"])
            .Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Name)
            .Select(person => person.Name)
            .ToList();

        Assert.Equal(new[] { $"{batch}-active", $"{batch}-deleted" }, withSoftDeleteDisabled);
    }

    private static async Task SeedAsync(string batch)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.AddRange(
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-active", Age = 10 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-deleted", Age = 10, IsDeleted = true },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-archived", Age = 10, IsArchived = true },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-both", Age = 10, IsDeleted = true, IsArchived = true });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
