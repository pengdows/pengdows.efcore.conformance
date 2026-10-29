using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class OwnedAttributeTests
{
    [Fact]
    [BehavioralCoverage("EF10-OWNED-ATTRIBUTE-ASYNC", "Microsoft.EntityFrameworkCore.OwnedAttribute", "Microsoft.EntityFrameworkCore.OwnedAttribute..ctor", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task OwnedAttribute_PersistsAndQueriesOwnedValuesAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var expected = CreateRecord("async-owned", 41);
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.OwnedAttributeRecords.Add(expected);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.OwnedAttributeRecords.AsNoTracking()
            .Where(record => record.Id == expected.Id && record.Value.Label == expected.Value.Label)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected.Value.Label, actual.Value.Label);
        Assert.Equal(expected.Value.Rank, actual.Value.Rank);
    }

    [Fact]
    [BehavioralCoverage("EF10-OWNED-ATTRIBUTE-SYNC", "Microsoft.EntityFrameworkCore.OwnedAttribute", "Microsoft.EntityFrameworkCore.OwnedAttribute..ctor", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    public async Task OwnedAttribute_PersistsAndQueriesOwnedValuesSynchronously()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var expected = CreateRecord("sync-owned", 73);
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.OwnedAttributeRecords.Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.OwnedAttributeRecords.AsNoTracking()
            .Where(record => record.Id == expected.Id && record.Value.Label == expected.Value.Label)
            .Single();
        Assert.Equal(expected.Value.Label, actual.Value.Label);
        Assert.Equal(expected.Value.Rank, actual.Value.Rank);
    }

    private static OwnedAttributeRecord CreateRecord(string label, int rank) => new()
    {
        Id = Guid.NewGuid(),
        Value = new OwnedAttributeValue { Label = label, Rank = rank }
    };
}
