using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class DatabaseLifecycleTests
{
    [Fact]
    [BehavioralCoverage("EF10-DATABASE-LIFECYCLE-ASYNC", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreatedAsync", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureDeletedAsync", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CanConnectAsync")]
    public async Task EnsureDeletedAsync_RemovesDatabaseAndReportsMissingDatabase()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            await using (var create = ProviderTestRuntime.CreateContext())
            {
                await create.Database.EnsureDeletedAsync(cancellationToken);
                Assert.True(await create.Database.EnsureCreatedAsync(cancellationToken));
                create.People.Add(new Person { Id = Guid.NewGuid(), Name = "Database lifecycle async", Age = 29 });
                Assert.Equal(1, await create.SaveChangesAsync(cancellationToken));
                Assert.True(await create.Database.EnsureDeletedAsync(cancellationToken));
                Assert.False(await create.Database.EnsureDeletedAsync(cancellationToken));
            }

            await using var verify = ProviderTestRuntime.CreateContext();
            Assert.False(await verify.Database.CanConnectAsync(cancellationToken));
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-DATABASE-LIFECYCLE-SYNC", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreated", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureDeleted", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CanConnect")]
    public async Task EnsureDeleted_RemovesDatabaseAndReportsMissingDatabaseSynchronously()
    {
        ProviderTestRuntime.RequireCapability("relational");

        try
        {
            await using (var create = ProviderTestRuntime.CreateContext())
            {
                create.Database.EnsureDeleted();
                Assert.True(create.Database.EnsureCreated());
                create.People.Add(new Person { Id = Guid.NewGuid(), Name = "Database lifecycle sync", Age = 31 });
                Assert.Equal(1, create.SaveChanges());
                Assert.True(create.Database.EnsureDeleted());
                Assert.False(create.Database.EnsureDeleted());
            }

            await using var verify = ProviderTestRuntime.CreateContext();
            Assert.False(verify.Database.CanConnect());
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }
}
