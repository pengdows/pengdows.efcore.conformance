using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class DatabaseLifecycleTests
{
    [Fact]
    [BehavioralCoverage("EF10-DATABASE-CAN-CONNECT-ASYNC", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CanConnectAsync")]
    public async Task CanConnectAsync_ReturnsTrueForInitializedDatabase()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.True(await context.Database.CanConnectAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-DATABASE-CAN-CONNECT-SYNC", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CanConnect")]
    public async Task CanConnect_ReturnsTrueForInitializedDatabaseSynchronously()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.True(context.Database.CanConnect());
    }
}
