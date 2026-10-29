using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class ComplexTypeTests
{
    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "Microsoft.EntityFrameworkCore.Metadata.Builders.ComplexPropertyBuilder.Property", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task ComplexProperty_RoundTripsAndCanBeQueriedAsynchronously()
    {
        var record = CreateRecord("101 Async Street", "Async City");
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.ComplexRecords.Add(record);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = await read.ComplexRecords.AsNoTracking()
            .Where(item => item.Id == record.Id && item.Address.City == "Async City")
            .Select(item => new { item.Address.Street, item.Address.City })
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal("101 Async Street", actual.Street);
        Assert.Equal("Async City", actual.City);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "Microsoft.EntityFrameworkCore.Metadata.Builders.ComplexPropertyBuilder.Property", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    public async Task ComplexProperty_RoundTripsAndCanBeQueriedSynchronously()
    {
        var record = CreateRecord("202 Sync Street", "Sync City");
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.ComplexRecords.Add(record);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = read.ComplexRecords.AsNoTracking()
            .Where(item => item.Id == record.Id && item.Address.City == "Sync City")
            .Select(item => new { item.Address.Street, item.Address.City })
            .Single();

        Assert.Equal("202 Sync Street", actual.Street);
        Assert.Equal("Sync City", actual.City);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-UPDATE-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.ComplexPropertyEntry.IsModified", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task ComplexProperty_NestedUpdatePersistsAsynchronously()
    {
        var record = CreateRecord("303 Update Street", "Original Async City");
        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.ComplexRecords.Add(record);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = await update.ComplexRecords.SingleAsync(
                item => item.Id == record.Id,
                TestContext.Current.CancellationToken);
            tracked.Address.City = "Updated Async City";
            Assert.True(update.Entry(tracked).ComplexProperty(item => item.Address).IsModified);
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.ComplexRecords.AsNoTracking().SingleAsync(
            item => item.Id == record.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(record.Address.Street, actual.Address.Street);
        Assert.Equal("Updated Async City", actual.Address.City);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-UPDATE-SYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.ComplexPropertyEntry.IsModified", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task ComplexProperty_NestedUpdatePersistsSynchronously()
    {
        var record = CreateRecord("404 Update Street", "Original Sync City");
        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.ComplexRecords.Add(record);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = update.ComplexRecords.Single(item => item.Id == record.Id);
            tracked.Address.City = "Updated Sync City";
            Assert.True(update.Entry(tracked).ComplexProperty(item => item.Address).IsModified);
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.ComplexRecords.AsNoTracking().Single(item => item.Id == record.Id);
        Assert.Equal(record.Address.Street, actual.Address.Street);
        Assert.Equal("Updated Sync City", actual.Address.City);
    }

    private static ComplexRecord CreateRecord(string street, string city) => new()
    {
        Id = Guid.NewGuid(),
        Address = new AddressInfo { Street = street, City = city }
    };
}
