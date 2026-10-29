using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class AcceptAllChangesTests
{
    [Fact]
    [BehavioralCoverage("EF10-PERSIST-SAVE-CHANGES-ACCEPT-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AcceptAllChanges", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.EntityState.Modified", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task SaveChangesAsyncFalse_PersistsButRetainsModifiedStateUntilAccepted()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var id = Guid.NewGuid();
        await SeedAsync(id, "before async");

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = await update.People.SingleAsync(
                item => item.Id == id, TestContext.Current.CancellationToken);
            tracked.Name = "after async";

            Assert.Equal(1, await update.SaveChangesAsync(acceptAllChangesOnSuccess: false,
                TestContext.Current.CancellationToken));
            Assert.Equal(EntityState.Modified, update.Entry(tracked).State);
            Assert.Equal("before async", update.Entry(tracked).Property(item => item.Name).OriginalValue);

            update.ChangeTracker.AcceptAllChanges();
            Assert.Equal(EntityState.Unchanged, update.Entry(tracked).State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("after async", await verify.People.AsNoTracking()
            .Where(item => item.Id == id).Select(item => item.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-SAVE-CHANGES-ACCEPT-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AcceptAllChanges", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.EntityState.Modified", "System.Linq.Queryable.Single")]
    public async Task SaveChangesFalse_PersistsButRetainsModifiedStateUntilAccepted()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "before sync");

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = update.People.Single(item => item.Id == id);
            tracked.Name = "after sync";

            Assert.Equal(1, update.SaveChanges(acceptAllChangesOnSuccess: false));
            Assert.Equal(EntityState.Modified, update.Entry(tracked).State);
            Assert.Equal("before sync", update.Entry(tracked).Property(item => item.Name).OriginalValue);

            update.ChangeTracker.AcceptAllChanges();
            Assert.Equal(EntityState.Unchanged, update.Entry(tracked).State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("after sync", verify.People.AsNoTracking()
            .Where(item => item.Id == id).Select(item => item.Name).Single());
    }

    private static async Task SeedAsync(Guid id, string name)
    {
        await using var seed = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
        seed.People.Add(new Person { Id = id, Name = name, Age = 35 });
        Assert.Equal(1, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
