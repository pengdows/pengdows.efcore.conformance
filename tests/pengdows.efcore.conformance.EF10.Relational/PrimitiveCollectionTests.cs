using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class PrimitiveCollectionTests
{
    [Fact]
    [BehavioralCoverage("EF10-PRIMITIVE-COLLECTION-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync", "System.Linq.Queryable.Where")]
    public async Task PrimitiveCollection_RoundTripsAndSupportsContainsQueryAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("primitive-collections");
        var expected = new[] { int.MinValue, -1, 0, 42, int.MaxValue };
        var record = new PrimitiveCollectionRecord { Id = Guid.NewGuid(), Values = [.. expected] };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.PrimitiveCollectionRecords.Add(record);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = await read.PrimitiveCollectionRecords.AsNoTracking()
            .Where(item => item.Id == record.Id && item.Values.Contains(42))
            .Select(item => item.Values)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-PRIMITIVE-COLLECTION-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single", "System.Linq.Queryable.Where")]
    public async Task PrimitiveCollection_RoundTripsAndSupportsContainsQuerySynchronously()
    {
        ProviderTestRuntime.RequireCapability("primitive-collections");
        var expected = new[] { int.MinValue, -1, 0, 42, int.MaxValue };
        var record = new PrimitiveCollectionRecord { Id = Guid.NewGuid(), Values = [.. expected] };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.PrimitiveCollectionRecords.Add(record);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = read.PrimitiveCollectionRecords.AsNoTracking()
            .Where(item => item.Id == record.Id && item.Values.Contains(42))
            .Select(item => item.Values)
            .Single();

        Assert.Equal(expected, actual);
    }
}
