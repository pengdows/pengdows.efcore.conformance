using Microsoft.EntityFrameworkCore;
using System.Data;
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

    [Fact]
    [BehavioralCoverage("EF10-DATABASE-ENSURE-CREATED-ASYNC", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreatedAsync")]
    public async Task EnsureCreatedAsync_ReturnsFalseWhenSchemaAlreadyExists()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.False(await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-DATABASE-ENSURE-CREATED-SYNC", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreated")]
    public async Task EnsureCreated_ReturnsFalseWhenSchemaAlreadyExistsSynchronously()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.False(context.Database.EnsureCreated());
    }

    [Fact]
    [BehavioralCoverage("EF10-DATABASE-CONNECTION-LIFECYCLE-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.OpenConnection", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.CloseConnection", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection")]
    public async Task ConnectionLifecycle_OpensAndClosesSynchronously()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        var connection = context.Database.GetDbConnection();

        Assert.Equal(ConnectionState.Closed, connection.State);
        context.Database.OpenConnection();
        Assert.Equal(ConnectionState.Open, connection.State);
        context.Database.CloseConnection();
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    [BehavioralCoverage("EF10-DATABASE-CONNECTION-LIFECYCLE-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.OpenConnectionAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.CloseConnectionAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection")]
    public async Task ConnectionLifecycle_OpensAndClosesAsynchronously()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        var connection = context.Database.GetDbConnection();

        Assert.Equal(ConnectionState.Closed, connection.State);
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ConnectionState.Open, connection.State);
        await context.Database.CloseConnectionAsync();
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    [BehavioralCoverage("EF10-CONTEXT-DISPOSE-SYNC", "Microsoft.EntityFrameworkCore.DbContext.Dispose", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.OpenConnection", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection")]
    public async Task Dispose_ClosesConnectionOpenedByContext()
    {
        var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        var connection = context.Database.GetDbConnection();
        context.Database.OpenConnection();

        Assert.Equal(ConnectionState.Open, connection.State);

        context.Dispose();

        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    [BehavioralCoverage("EF10-CONTEXT-DISPOSE-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.DisposeAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.OpenConnectionAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection")]
    public async Task DisposeAsync_ClosesConnectionOpenedByContext()
    {
        var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ConnectionState.Open, connection.State);

        await context.DisposeAsync();

        Assert.Equal(ConnectionState.Closed, connection.State);
    }
}
