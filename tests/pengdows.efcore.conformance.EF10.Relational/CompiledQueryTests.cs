using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class CompiledQueryTests
{
    [Fact]
    [BehavioralCoverage("EF10-COMPILED-QUERY-SYNC", "Microsoft.EntityFrameworkCore.EF.CompileQuery")]
    public async Task CompileQuery_ExecutesParameterizedQuerySynchronously()
    {
        var prefix = Guid.NewGuid().ToString("N");
        await SeedAsync(prefix);

        var compiled = EF.CompileQuery(
            (ConformanceDbContext context, string namePrefix, int minimumAge) => context.People
                .Where(person => person.Name.StartsWith(namePrefix) && person.Age >= minimumAge)
                .OrderBy(person => person.Age)
                .Select(person => person.Name));

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = compiled(query, prefix, 20).ToArray();

        Assert.Equal(new[] { $"{prefix}-adult", $"{prefix}-senior" }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPILED-QUERY-ASYNC", "Microsoft.EntityFrameworkCore.EF.CompileAsyncQuery")]
    public async Task CompileAsyncQuery_ExecutesParameterizedQueryAsynchronously()
    {
        var prefix = Guid.NewGuid().ToString("N");
        await SeedAsync(prefix);

        var compiled = EF.CompileAsyncQuery(
            (ConformanceDbContext context, string namePrefix, int minimumAge) => context.People
                .Where(person => person.Name.StartsWith(namePrefix) && person.Age >= minimumAge)
                .OrderBy(person => person.Age)
                .Select(person => person.Name));

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = new List<string>();
        await foreach (var name in compiled(query, prefix, 20)
                           .WithCancellation(TestContext.Current.CancellationToken))
        {
            actual.Add(name);
        }

        Assert.Equal(new[] { $"{prefix}-adult", $"{prefix}-senior" }, actual);
    }

    private static async Task SeedAsync(string prefix)
    {
        await using var seed = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
        seed.People.AddRange(
            new Person { Id = Guid.NewGuid(), Name = $"{prefix}-child", Age = 12 },
            new Person { Id = Guid.NewGuid(), Name = $"{prefix}-adult", Age = 30 },
            new Person { Id = Guid.NewGuid(), Name = $"{prefix}-senior", Age = 65 });
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
