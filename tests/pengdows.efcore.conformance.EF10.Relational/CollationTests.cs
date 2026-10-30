using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("case-insensitive-collation")]
public sealed class CollationTests
{
    [Fact]
    [BehavioralCoverage(
        "EF10-COLLATE-ASYNC",
        "Microsoft.EntityFrameworkCore.RelationalDbFunctionsExtensions.Collate",
        "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task Collate_MatchesTextUsingTheDeclaredCaseInsensitiveCollationAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("case-insensitive-collation");
        var marker = $"EF10_COLLATE_{Guid.NewGuid():N}";
        var id = Guid.NewGuid();
        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.People.Add(new Person { Id = id, Name = marker, Age = 30 });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People
            .Where(person => EF.Functions.Collate(person.Name, ProviderTestRuntime.CaseInsensitiveCollation) == marker.ToLowerInvariant())
            .Select(person => person.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(id, actual);
    }

    [Fact]
    [BehavioralCoverage(
        "EF10-COLLATE-SYNC",
        "Microsoft.EntityFrameworkCore.RelationalDbFunctionsExtensions.Collate",
        "System.Linq.Queryable.Single")]
    public async Task Collate_MatchesTextUsingTheDeclaredCaseInsensitiveCollationSynchronously()
    {
        ProviderTestRuntime.RequireCapability("case-insensitive-collation");
        var marker = $"EF10_COLLATE_{Guid.NewGuid():N}";
        var id = Guid.NewGuid();
        using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.People.Add(new Person { Id = id, Name = marker, Age = 30 });
            seed.SaveChanges();
        }

        using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.People
            .Where(person => EF.Functions.Collate(person.Name, ProviderTestRuntime.CaseInsensitiveCollation) == marker.ToLowerInvariant())
            .Select(person => person.Id)
            .Single();

        Assert.Equal(id, actual);
    }
}
