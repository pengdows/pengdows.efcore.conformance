using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class DatabaseFunctionTests
{
    [Fact]
    [CapabilityRequirement("least-greatest-functions")]
    [BehavioralCoverage("EF10-DB-FUNCTION-LEAST-GREATEST-SYNC", "Microsoft.EntityFrameworkCore.RelationalDbFunctionsExtensions.Least", "Microsoft.EntityFrameworkCore.RelationalDbFunctionsExtensions.Greatest", "System.Linq.Queryable.Select", "System.Linq.Queryable.Single")]
    public async Task LeastAndGreatest_ReturnExpectedValuesSynchronously()
    {
        ProviderTestRuntime.RequireCapability("least-greatest-functions");
        var marker = await SeedOneAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        var result = context.People
            .Where(person => person.Name == marker)
            .Select(_ => new
            {
                Least = EF.Functions.Least(7, -2, 11, 4),
                Greatest = EF.Functions.Greatest(7, -2, 11, 4)
            })
            .Single();

        Assert.Equal(-2, result.Least);
        Assert.Equal(11, result.Greatest);
    }

    [Fact]
    [CapabilityRequirement("least-greatest-functions")]
    [BehavioralCoverage("EF10-DB-FUNCTION-LEAST-GREATEST-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDbFunctionsExtensions.Least", "Microsoft.EntityFrameworkCore.RelationalDbFunctionsExtensions.Greatest", "System.Linq.Queryable.Select", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task LeastAndGreatest_ReturnExpectedValuesAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("least-greatest-functions");
        var marker = await SeedOneAsync();
        await using var context = ProviderTestRuntime.CreateContext();

        var result = await context.People
            .Where(person => person.Name == marker)
            .Select(_ => new
            {
                Least = EF.Functions.Least(7, -2, 11, 4),
                Greatest = EF.Functions.Greatest(7, -2, 11, 4)
            })
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(-2, result.Least);
        Assert.Equal(11, result.Greatest);
    }

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

    private static async Task<string> SeedOneAsync()
    {
        var marker = $"Least Greatest {Guid.NewGuid():N}";
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(NewPerson(marker));
        Assert.Equal(1, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
        return marker;
    }

    private static Person NewPerson(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Age = 35
    };
}
