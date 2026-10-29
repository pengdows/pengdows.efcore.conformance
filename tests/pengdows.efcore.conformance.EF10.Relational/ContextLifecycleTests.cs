using System.Data;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class ContextLifecycleTests
{
    [Fact]
    [BehavioralCoverage("EF10-CONTEXT-DISPOSE-SYNC", "Microsoft.EntityFrameworkCore.DbContext..ctor", "Microsoft.EntityFrameworkCore.DbContext.Database", "Microsoft.EntityFrameworkCore.DbContext.Dispose", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.OpenConnection", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.CloseConnection", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection")]
    public void Dispose_ClosesTheContextOwnedConnection()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var context = ProviderTestRuntime.CreateContext();
        context.Database.OpenConnection();
        var connection = context.Database.GetDbConnection();

        Assert.Equal(ConnectionState.Open, connection.State);
        context.Database.CloseConnection();
        Assert.Equal(ConnectionState.Closed, connection.State);
        context.Database.OpenConnection();

        context.Dispose();

        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    [BehavioralCoverage("EF10-CONTEXT-DISPOSE-ASYNC", "Microsoft.EntityFrameworkCore.DbContext..ctor", "Microsoft.EntityFrameworkCore.DbContext.Database", "Microsoft.EntityFrameworkCore.DbContext.DisposeAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.OpenConnectionAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.CloseConnectionAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection")]
    public async Task DisposeAsync_ClosesTheContextOwnedConnection()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var context = ProviderTestRuntime.CreateContext();
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        var connection = context.Database.GetDbConnection();

        Assert.Equal(ConnectionState.Open, connection.State);
        await context.Database.CloseConnectionAsync();
        Assert.Equal(ConnectionState.Closed, connection.State);
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);

        await context.DisposeAsync();

        Assert.Equal(ConnectionState.Closed, connection.State);
    }
}
