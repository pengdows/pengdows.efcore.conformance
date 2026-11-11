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

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-VALUE-EQUALITY-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "System.Linq.Queryable.Where", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task ComplexPropertyEquality_ComparesValuesAsynchronously()
    {
        var matching = CreateRecord("1 Equality Street", "Value City");
        var other = CreateRecord("2 Equality Street", "Value City", new AddressInfo { Street = "Different Street", City = "Value City" });
        await SeedRecordsAsync(matching, other);
        var ids = new[] { matching.Id, other.Id };

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = await query.ComplexRecords.AsNoTracking()
            .Where(record => ids.Contains(record.Id) && record.Address == record.ComparisonAddress)
            .Select(record => record.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { matching.Id }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-VALUE-EQUALITY-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "System.Linq.Queryable.Where", "System.Linq.Enumerable.ToArray")]
    public async Task ComplexPropertyEquality_ComparesValuesSynchronously()
    {
        var matching = CreateRecord("3 Equality Street", "Sync Value City");
        var other = CreateRecord("4 Equality Street", "Sync Value City", new AddressInfo { Street = "Different Sync Street", City = "Sync Value City" });
        await SeedRecordsAsync(matching, other);
        var ids = new[] { matching.Id, other.Id };

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = query.ComplexRecords.AsNoTracking()
            .Where(record => ids.Contains(record.Id) && record.Address == record.ComparisonAddress)
            .Select(record => record.Id)
            .ToArray();

        Assert.Equal(new[] { matching.Id }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-ASSIGNMENT-COPIES-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task AssigningOneComplexPropertyToAnother_CopiesItsValuesAsynchronously()
    {
        var record = CreateRecord("Assignment Source", "Source City", new AddressInfo { Street = "Destination Before", City = "Destination City" });
        await SeedRecordsAsync(record);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = await update.ComplexRecords.SingleAsync(item => item.Id == record.Id, TestContext.Current.CancellationToken);
            tracked.ComparisonAddress = tracked.Address;
            Assert.Equal("Source City", tracked.ComparisonAddress.City);
            await update.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.ComplexRecords.AsNoTracking().SingleAsync(item => item.Id == record.Id, TestContext.Current.CancellationToken);
        Assert.Equal("Source City", actual.Address.City);
        Assert.Equal("Source City", actual.ComparisonAddress.City);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-ASSIGNMENT-COPIES-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task AssigningOneComplexPropertyToAnother_CopiesItsValuesSynchronously()
    {
        var record = CreateRecord("Sync Assignment Source", "Sync Source City", new AddressInfo { Street = "Sync Destination Before", City = "Sync Destination City" });
        await SeedRecordsAsync(record);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = update.ComplexRecords.Single(item => item.Id == record.Id);
            tracked.ComparisonAddress = tracked.Address;
            Assert.Equal("Sync Source City", tracked.ComparisonAddress.City);
            update.SaveChanges();
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.ComplexRecords.AsNoTracking().Single(item => item.Id == record.Id);
        Assert.Equal("Sync Source City", actual.Address.City);
        Assert.Equal("Sync Source City", actual.ComparisonAddress.City);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-OPTIONAL-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToArrayAsync")]
    public async Task OptionalComplexProperty_RoundTripsNullAndValueAsynchronously()
    {
        var records = CreateOptionalRecords();
        await SeedOptionalRecordsAsync(records);

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = await query.OptionalComplexRecords.AsNoTracking()
            .Where(record => records.Select(seed => seed.Id).Contains(record.Id))
            .OrderBy(record => record.Id)
            .Select(record => new OptionalAddressResult(record.Id, record.BillingAddress == null ? null : record.BillingAddress.Street, record.BillingAddress == null ? null : record.BillingAddress.City))
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            records.OrderBy(record => record.Id).Select(record => new OptionalAddressResult(record.Id, record.BillingAddress?.Street, record.BillingAddress?.City)).ToArray(),
            actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-OPTIONAL-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.ToArray")]
    public async Task OptionalComplexProperty_RoundTripsNullAndValueSynchronously()
    {
        var records = CreateOptionalRecords();
        await SeedOptionalRecordsAsync(records);

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = query.OptionalComplexRecords.AsNoTracking()
            .Where(record => records.Select(seed => seed.Id).Contains(record.Id))
            .OrderBy(record => record.Id)
            .Select(record => new OptionalAddressResult(record.Id, record.BillingAddress == null ? null : record.BillingAddress.Street, record.BillingAddress == null ? null : record.BillingAddress.City))
            .ToArray();

        Assert.Equal(
            records.OrderBy(record => record.Id).Select(record => new OptionalAddressResult(record.Id, record.BillingAddress?.Street, record.BillingAddress?.City)).ToArray(),
            actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-STRUCT-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task StructComplexProperty_RoundTripsAsynchronously()
    {
        var record = CreateStructRecord();
        await SeedStructRecordAsync(record);

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = await query.StructComplexRecords.AsNoTracking()
            .Where(item => item.Id == record.Id)
            .Select(item => new AddressResult(item.Address.Street, item.Address.City))
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new AddressResult(record.Address.Street, record.Address.City), actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-STRUCT-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    public async Task StructComplexProperty_RoundTripsSynchronously()
    {
        var record = CreateStructRecord();
        await SeedStructRecordAsync(record);

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = query.StructComplexRecords.AsNoTracking()
            .Where(item => item.Id == record.Id)
            .Select(item => new AddressResult(item.Address.Street, item.Address.City))
            .Single();

        Assert.Equal(new AddressResult(record.Address.Street, record.Address.City), actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-STRUCT-COLLECTION-ROUNDTRIP", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    public void ComplexTypeCollectionOfStructs_RoundTripsSynchronously()
    {
        ProviderTestRuntime.RequireCapability("json-mapping");
        var options = new DbContextOptionsBuilder<StructCollectionProbeContext>();
        ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString);
        using var context = new StructCollectionProbeContext(options.Options);

        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        var record = new StructCollectionProbeRecord
        {
            Id = Guid.NewGuid(),
            Addresses = [new AddressValue { Street = "Struct collection", City = "Negative contract" }]
        };
        context.StructCollectionProbeRecords.Add(record);
        Assert.Equal(1, context.SaveChanges());
        context.ChangeTracker.Clear();

        var actual = context.StructCollectionProbeRecords.AsNoTracking().Single(item => item.Id == record.Id);
        var address = Assert.Single(actual.Addresses);
        Assert.Equal("Struct collection", address.Street);
        Assert.Equal("Negative contract", address.City);
    }

    [Fact]
    [BehavioralCoverage("EF10-COMPLEX-TYPE-STRUCT-COLLECTION-ROUNDTRIP-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task ComplexTypeCollectionOfStructs_RoundTripsAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("json-mapping");
        var options = new DbContextOptionsBuilder<StructCollectionProbeContext>();
        ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString);
        await using var context = new StructCollectionProbeContext(options.Options);

        await context.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
        await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var record = new StructCollectionProbeRecord
        {
            Id = Guid.NewGuid(),
            Addresses = [new AddressValue { Street = "Async struct collection", City = "Async negative contract" }]
        };
        context.StructCollectionProbeRecords.Add(record);
        Assert.Equal(1, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
        context.ChangeTracker.Clear();

        var actual = await context.StructCollectionProbeRecords.AsNoTracking()
            .SingleAsync(item => item.Id == record.Id, TestContext.Current.CancellationToken);
        var address = Assert.Single(actual.Addresses);
        Assert.Equal("Async struct collection", address.Street);
        Assert.Equal("Async negative contract", address.City);
    }

    private static ComplexRecord CreateRecord(string street, string city, AddressInfo? comparisonAddress = null) => new()
    {
        Id = Guid.NewGuid(),
        Address = new AddressInfo { Street = street, City = city },
        ComparisonAddress = comparisonAddress ?? new AddressInfo { Street = street, City = city }
    };

    private static async Task SeedRecordsAsync(params ComplexRecord[] records)
    {
        await using var write = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
        write.ComplexRecords.AddRange(records);
        await write.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static OptionalComplexRecord[] CreateOptionalRecords() =>
    [
        new OptionalComplexRecord { Id = Guid.NewGuid() },
        new OptionalComplexRecord { Id = Guid.NewGuid(), BillingAddress = new AddressInfo { Street = "Optional Street", City = "Optional City" } }
    ];

    private static async Task SeedOptionalRecordsAsync(IEnumerable<OptionalComplexRecord> records)
    {
        await using var write = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
        write.OptionalComplexRecords.AddRange(records);
        await write.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static StructComplexRecord CreateStructRecord() => new()
    {
        Id = Guid.NewGuid(),
        Address = new AddressValue { Street = "Struct Street", City = "Struct City" }
    };

    private static async Task SeedStructRecordAsync(StructComplexRecord record)
    {
        await using var write = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
        write.StructComplexRecords.Add(record);
        await write.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed record AddressResult(string Street, string City);

    private sealed record OptionalAddressResult(Guid Id, string? Street, string? City);
}

internal sealed class StructCollectionProbeContext(DbContextOptions<StructCollectionProbeContext> options) : DbContext(options)
{
    public DbSet<StructCollectionProbeRecord> StructCollectionProbeRecords => Set<StructCollectionProbeRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StructCollectionProbeRecord>(entity =>
        {
            entity.ToTable("ef10conformance_structcollectionprobe");
            entity.HasKey(record => record.Id);
            entity.ComplexProperty(record => record.Addresses, addresses => addresses.ToJson("addresses"));
        });
    }
}

internal sealed class StructCollectionProbeRecord
{
    public Guid Id { get; set; }

    public List<AddressValue> Addresses { get; set; } = [];
}
