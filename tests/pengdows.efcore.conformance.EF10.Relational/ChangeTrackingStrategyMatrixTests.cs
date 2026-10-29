using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class ChangeTrackingStrategyMatrixTests
{
    [Fact]
    [BehavioralCoverage("EF10-TRACKING-SNAPSHOT-STRATEGY-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy.Snapshot", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task SnapshotStrategy_DetectsAndPersistsAsyncChanges()
    {
        var record = new SnapshotStrategyRecord { Id = Guid.NewGuid(), Name = "before snapshot async" };
        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.SnapshotStrategyRecords.Add(record);
            Assert.Equal(1, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = await update.SnapshotStrategyRecords.SingleAsync(
                item => item.Id == record.Id, TestContext.Current.CancellationToken);
            tracked.Name = "after snapshot async";
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("after snapshot async", await verify.SnapshotStrategyRecords.AsNoTracking()
            .Where(item => item.Id == record.Id).Select(item => item.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-SNAPSHOT-STRATEGY-SYNC", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy.Snapshot", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task SnapshotStrategy_DetectsAndPersistsSynchronousChanges()
    {
        var record = new SnapshotStrategyRecord { Id = Guid.NewGuid(), Name = "before snapshot sync" };
        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.SnapshotStrategyRecords.Add(record);
            Assert.Equal(1, seed.SaveChanges());
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = update.SnapshotStrategyRecords.Single(item => item.Id == record.Id);
            tracked.Name = "after snapshot sync";
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("after snapshot sync", verify.SnapshotStrategyRecords.AsNoTracking()
            .Where(item => item.Id == record.Id).Select(item => item.Name).Single());
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-CHANGING-STRATEGIES-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy.ChangingAndChangedNotifications", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy.ChangingAndChangedNotificationsWithOriginalValues", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task ChangingAndChangedStrategies_PersistAsyncChangesWithoutSnapshotDetection()
    {
        var changing = new ChangingStrategyRecord { Id = Guid.NewGuid(), Name = "before changing async" };
        var withOriginals = new ChangingStrategyWithOriginalsRecord { Id = Guid.NewGuid(), Name = "before originals async" };
        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.AddRange(changing, withOriginals);
            Assert.Equal(2, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var trackedChanging = await update.ChangingStrategyRecords.SingleAsync(
                item => item.Id == changing.Id, TestContext.Current.CancellationToken);
            var trackedWithOriginals = await update.ChangingStrategyWithOriginalsRecords.SingleAsync(
                item => item.Id == withOriginals.Id, TestContext.Current.CancellationToken);
            update.ChangeTracker.AutoDetectChangesEnabled = false;
            trackedChanging.Name = "after changing async";
            trackedWithOriginals.Name = "after originals async";
            Assert.Equal(EntityState.Modified, update.Entry(trackedChanging).State);
            Assert.Equal(EntityState.Modified, update.Entry(trackedWithOriginals).State);
            Assert.Equal(2, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("after changing async", await verify.ChangingStrategyRecords.AsNoTracking()
            .Where(item => item.Id == changing.Id).Select(item => item.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal("after originals async", await verify.ChangingStrategyWithOriginalsRecords.AsNoTracking()
            .Where(item => item.Id == withOriginals.Id).Select(item => item.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-CHANGING-STRATEGIES-SYNC", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy.ChangingAndChangedNotifications", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy.ChangingAndChangedNotificationsWithOriginalValues", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task ChangingAndChangedStrategies_PersistSyncChangesWithoutSnapshotDetection()
    {
        var changing = new ChangingStrategyRecord { Id = Guid.NewGuid(), Name = "before changing sync" };
        var withOriginals = new ChangingStrategyWithOriginalsRecord { Id = Guid.NewGuid(), Name = "before originals sync" };
        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.AddRange(changing, withOriginals);
            Assert.Equal(2, seed.SaveChanges());
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var trackedChanging = update.ChangingStrategyRecords.Single(item => item.Id == changing.Id);
            var trackedWithOriginals = update.ChangingStrategyWithOriginalsRecords.Single(item => item.Id == withOriginals.Id);
            update.ChangeTracker.AutoDetectChangesEnabled = false;
            trackedChanging.Name = "after changing sync";
            trackedWithOriginals.Name = "after originals sync";
            Assert.Equal(EntityState.Modified, update.Entry(trackedChanging).State);
            Assert.Equal(EntityState.Modified, update.Entry(trackedWithOriginals).State);
            Assert.Equal(2, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("after changing sync", verify.ChangingStrategyRecords.AsNoTracking()
            .Where(item => item.Id == changing.Id).Select(item => item.Name).Single());
        Assert.Equal("after originals sync", verify.ChangingStrategyWithOriginalsRecords.AsNoTracking()
            .Where(item => item.Id == withOriginals.Id).Select(item => item.Name).Single());
    }
}
