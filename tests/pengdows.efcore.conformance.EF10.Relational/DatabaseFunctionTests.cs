using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class DatabaseFunctionTests
{
    [Fact]
    [BehavioralCoverage("EF10-DB-FUNCTION-RANDOM-SYNC", "Microsoft.EntityFrameworkCore.DbFunctionsExtensions.Random", "System.Linq.Queryable.Select", "System.Linq.Queryable.ToArray")]
    public async Task Random_ReturnsValuesStrictlyBetweenZeroAndOneSynchronously()
    {
        var marker = await SeedAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        var values = context.People
            .Where(person => person.Name.StartsWith(marker))
            .OrderBy(person => person.Id)
            .Select(_ => EF.Functions.Random())
            .ToArray();

        Assert.Equal(3, values.Length);
        Assert.All(values, value => Assert.InRange(value, double.Epsilon, Math.BitDecrement(1d)));
    }

    [Fact]
    [BehavioralCoverage("EF10-DB-FUNCTION-RANDOM-ASYNC", "Microsoft.EntityFrameworkCore.DbFunctionsExtensions.Random", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync", "System.Linq.Queryable.Select", "System.Linq.Queryable.OrderBy")]
    public async Task Random_ReturnsValuesStrictlyBetweenZeroAndOneAsynchronously()
    {
        var marker = await SeedAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        var values = await context.People
            .Where(person => person.Name.StartsWith(marker))
            .OrderBy(person => person.Id)
            .Select(_ => EF.Functions.Random())
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(3, values.Length);
        Assert.All(values, value => Assert.InRange(value, double.Epsilon, Math.BitDecrement(1d)));
    }

    private static async Task<string> SeedAsync()
    {
        var marker = $"Random {Guid.NewGuid():N}";
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.AddRange(
            NewPerson($"{marker} one"),
            NewPerson($"{marker} two"),
            NewPerson($"{marker} three"));
        Assert.Equal(3, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
        return marker;
    }

    private static Person NewPerson(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Age = 35
    };
}
