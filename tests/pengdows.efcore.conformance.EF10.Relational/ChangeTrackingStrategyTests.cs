using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class ChangeTrackingStrategyTests
{
    [Fact]
    [BehavioralCoverage("EF10-TRACKING-NOTIFICATION-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy.ChangedNotifications", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task ChangedNotifications_PersistsPropertyChangeWithoutSnapshotDetection()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var record = new NotifyingRecord { Id = Guid.NewGuid(), Name = "before async" };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.NotifyingRecords.Add(record);
            Assert.Equal(1, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = await update.NotifyingRecords.SingleAsync(
                item => item.Id == record.Id, TestContext.Current.CancellationToken);
            update.ChangeTracker.AutoDetectChangesEnabled = false;
            tracked.Name = "after async";
            Assert.Equal(EntityState.Modified, update.Entry(tracked).State);
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("after async", await verify.NotifyingRecords.AsNoTracking()
            .Where(item => item.Id == record.Id).Select(item => item.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-NOTIFICATION-SYNC", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy", "Microsoft.EntityFrameworkCore.ChangeTrackingStrategy.ChangedNotifications", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task ChangedNotifications_PersistsPropertyChangeSynchronously()
    {
        var record = new NotifyingRecord { Id = Guid.NewGuid(), Name = "before sync" };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.NotifyingRecords.Add(record);
            Assert.Equal(1, seed.SaveChanges());
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = update.NotifyingRecords.Single(item => item.Id == record.Id);
            update.ChangeTracker.AutoDetectChangesEnabled = false;
            tracked.Name = "after sync";
            Assert.Equal(EntityState.Modified, update.Entry(tracked).State);
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("after sync", verify.NotifyingRecords.AsNoTracking()
            .Where(item => item.Id == record.Id).Select(item => item.Name).Single());
    }
}
