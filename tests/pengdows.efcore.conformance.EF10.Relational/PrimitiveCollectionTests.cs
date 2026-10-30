using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("primitive-collections")]
public sealed class PrimitiveCollectionTests
{
    [Fact]
    [BehavioralCoverage("EF10-PRIMITIVE-COLLECTION-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync", "System.Linq.Queryable.Where")]
    [NativeTypeCoverage("INTEGER[]", typeof(int[]), "EF10-PRIMITIVE-COLLECTION-ASYNC", "Npgsql EF Core PostgreSQL")]
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
    [NativeTypeCoverage("INTEGER[]", typeof(int[]), "EF10-PRIMITIVE-COLLECTION-SYNC", "Npgsql EF Core PostgreSQL")]
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

    [Fact]
    [BehavioralCoverage("EF10-PRIMITIVE-COLLECTION-MUTATION-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.DetectChanges", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task PrimitiveCollection_InPlaceElementMutationPersistsAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("primitive-collections");
        var record = new PrimitiveCollectionRecord { Id = Guid.NewGuid(), Values = [10, 20, 30] };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.PrimitiveCollectionRecords.Add(record);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = await update.PrimitiveCollectionRecords.SingleAsync(
                item => item.Id == record.Id,
                TestContext.Current.CancellationToken);
            tracked.Values[1] = 99;

            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.PrimitiveCollectionRecords.AsNoTracking()
            .Where(item => item.Id == record.Id)
            .Select(item => item.Values)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { 10, 99, 30 }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-PRIMITIVE-COLLECTION-MUTATION-SYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.DetectChanges", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task PrimitiveCollection_InPlaceElementMutationPersistsSynchronously()
    {
        ProviderTestRuntime.RequireCapability("primitive-collections");
        var record = new PrimitiveCollectionRecord { Id = Guid.NewGuid(), Values = [11, 21, 31] };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.PrimitiveCollectionRecords.Add(record);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = update.PrimitiveCollectionRecords.Single(item => item.Id == record.Id);
            tracked.Values[1] = 98;

            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.PrimitiveCollectionRecords.AsNoTracking()
            .Where(item => item.Id == record.Id)
            .Select(item => item.Values)
            .Single();
        Assert.Equal(new[] { 11, 98, 31 }, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-PRIMITIVE-COLLECTION-NULLABLE-ELEMENTS-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task PrimitiveCollection_NullableElementsRoundTripAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("primitive-collections");
        int?[] expected = [null, int.MinValue, 0, 42, int.MaxValue];
        var record = new PrimitiveCollectionRecord { Id = Guid.NewGuid(), OptionalValues = [.. expected] };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.PrimitiveCollectionRecords.Add(record);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.PrimitiveCollectionRecords.AsNoTracking()
            .Where(item => item.Id == record.Id)
            .Select(item => item.OptionalValues)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-PRIMITIVE-COLLECTION-NULLABLE-ELEMENTS-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    public async Task PrimitiveCollection_NullableElementsRoundTripSynchronously()
    {
        ProviderTestRuntime.RequireCapability("primitive-collections");
        int?[] expected = [null, int.MinValue, 0, 42, int.MaxValue];
        var record = new PrimitiveCollectionRecord { Id = Guid.NewGuid(), OptionalValues = [.. expected] };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.PrimitiveCollectionRecords.Add(record);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.PrimitiveCollectionRecords.AsNoTracking()
            .Where(item => item.Id == record.Id)
            .Select(item => item.OptionalValues)
            .Single();
        Assert.Equal(expected, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-PRIMITIVE-COLLECTION-NULL-PREDICATE-ASYNC", "System.Linq.Queryable.Where", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task PrimitiveCollection_QueriesForNullElementAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("primitive-collections");
        var id = Guid.NewGuid();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.PrimitiveCollectionRecords.Add(new PrimitiveCollectionRecord
            {
                Id = id,
                OptionalValues = [null, 16]
            });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        int? searchedValue = null;
        await using var query = ProviderTestRuntime.CreateContext();
        var actualId = await query.PrimitiveCollectionRecords.AsNoTracking()
            .Where(record => record.Id == id && record.OptionalValues.Contains(searchedValue))
            .Select(record => record.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(id, actualId);
    }

    [Fact]
    [BehavioralCoverage("EF10-PRIMITIVE-COLLECTION-NULL-PREDICATE-SYNC", "System.Linq.Queryable.Where", "System.Linq.Queryable.Single")]
    public async Task PrimitiveCollection_QueriesForNullElementSynchronously()
    {
        ProviderTestRuntime.RequireCapability("primitive-collections");
        var id = Guid.NewGuid();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.PrimitiveCollectionRecords.Add(new PrimitiveCollectionRecord
            {
                Id = id,
                OptionalValues = [null, 17]
            });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        int? searchedValue = null;
        await using var query = ProviderTestRuntime.CreateContext();
        var actualId = query.PrimitiveCollectionRecords.AsNoTracking()
            .Where(record => record.Id == id && record.OptionalValues.Contains(searchedValue))
            .Select(record => record.Id)
            .Single();

        Assert.Equal(id, actualId);
    }

    [Fact]
    [BehavioralCoverage("EF10-PRIMITIVE-COLLECTION-INDEX-LENGTH-ASYNC", "System.Linq.Queryable.Where", "System.Linq.Queryable.Select", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task PrimitiveCollection_CanFilterByLengthAndProjectAnIndexedElementAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("primitive-collections");
        var id = Guid.NewGuid();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.PrimitiveCollectionRecords.Add(new PrimitiveCollectionRecord
            {
                Id = id,
                Values = [10, 20, 30, 40, 50]
            });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var query = ProviderTestRuntime.CreateContext();
        var value = await query.PrimitiveCollectionRecords.AsNoTracking()
            .Where(record => record.Id == id && record.Values.Count == 5)
            .Select(record => record.Values[2])
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(30, value);
    }

    [Fact]
    [BehavioralCoverage("EF10-PRIMITIVE-COLLECTION-INDEX-LENGTH-SYNC", "System.Linq.Queryable.Where", "System.Linq.Queryable.Select", "System.Linq.Queryable.Single")]
    public async Task PrimitiveCollection_CanFilterByLengthAndProjectAnIndexedElementSynchronously()
    {
        ProviderTestRuntime.RequireCapability("primitive-collections");
        var id = Guid.NewGuid();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.PrimitiveCollectionRecords.Add(new PrimitiveCollectionRecord
            {
                Id = id,
                Values = [10, 20, 30, 40, 50]
            });
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var query = ProviderTestRuntime.CreateContext();
        var value = query.PrimitiveCollectionRecords.AsNoTracking()
            .Where(record => record.Id == id && record.Values.Count == 5)
            .Select(record => record.Values[2])
            .Single();

        Assert.Equal(30, value);
    }
}
