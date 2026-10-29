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

    private static ComplexRecord CreateRecord(string street, string city) => new()
    {
        Id = Guid.NewGuid(),
        Address = new AddressInfo { Street = street, City = city }
    };
}
