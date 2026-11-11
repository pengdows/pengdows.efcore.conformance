using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class ContextLifecycleTests
{
    [Fact]
    [BehavioralCoverage("EF10-DBCONTEXT-POOL-RESET-ASYNC", "Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions.AddDbContextPool", "Microsoft.EntityFrameworkCore.DbContext.ContextId", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.QueryTrackingBehavior", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AutoDetectChangesEnabled")]
    public async Task PooledContext_ResetsPublicStateBeforeAsynchronousReuse()
    {
        var services = new ServiceCollection();
        services.AddDbContextPool<ConformanceDbContext>(
            options => ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString),
            poolSize: 1);
        await using var serviceProvider = services.BuildServiceProvider();
        Guid firstInstanceId;
        int? defaultTimeout;

        await using (var firstScope = serviceProvider.CreateAsyncScope())
        {
            var first = firstScope.ServiceProvider.GetRequiredService<ConformanceDbContext>();
            firstInstanceId = first.ContextId.InstanceId;
            defaultTimeout = first.Database.GetCommandTimeout();
            first.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            first.ChangeTracker.AutoDetectChangesEnabled = false;
            first.Database.SetCommandTimeout(137);
            first.People.Attach(new Person { Id = Guid.NewGuid(), Name = "Pooled context state", Age = 1 });
            Assert.Single(first.ChangeTracker.Entries());
        }

        await using var secondScope = serviceProvider.CreateAsyncScope();
        var second = secondScope.ServiceProvider.GetRequiredService<ConformanceDbContext>();
        Assert.Equal(firstInstanceId, second.ContextId.InstanceId);
        Assert.Equal(QueryTrackingBehavior.TrackAll, second.ChangeTracker.QueryTrackingBehavior);
        Assert.True(second.ChangeTracker.AutoDetectChangesEnabled);
        Assert.Empty(second.ChangeTracker.Entries());
        Assert.Equal(defaultTimeout, second.Database.GetCommandTimeout());
    }

    [Fact]
    [BehavioralCoverage("EF10-DBCONTEXT-POOL-RESET-SYNC", "Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions.AddDbContextPool", "Microsoft.EntityFrameworkCore.DbContext.ContextId", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.QueryTrackingBehavior", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AutoDetectChangesEnabled")]
    public async Task PooledContext_ResetsPublicStateBeforeSynchronousReuse()
    {
        var services = new ServiceCollection();
        services.AddDbContextPool<ConformanceDbContext>(
            options => ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString),
            poolSize: 1);
        await using var serviceProvider = services.BuildServiceProvider();
        Guid firstInstanceId;
        int? defaultTimeout;

        using (var firstScope = serviceProvider.CreateScope())
        {
            var first = firstScope.ServiceProvider.GetRequiredService<ConformanceDbContext>();
            firstInstanceId = first.ContextId.InstanceId;
            defaultTimeout = first.Database.GetCommandTimeout();
            first.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            first.ChangeTracker.AutoDetectChangesEnabled = false;
            first.Database.SetCommandTimeout(137);
            first.People.Attach(new Person { Id = Guid.NewGuid(), Name = "Pooled context state sync", Age = 1 });
            Assert.Single(first.ChangeTracker.Entries());
        }

        using var secondScope = serviceProvider.CreateScope();
        var second = secondScope.ServiceProvider.GetRequiredService<ConformanceDbContext>();
        Assert.Equal(firstInstanceId, second.ContextId.InstanceId);
        Assert.Equal(QueryTrackingBehavior.TrackAll, second.ChangeTracker.QueryTrackingBehavior);
        Assert.True(second.ChangeTracker.AutoDetectChangesEnabled);
        Assert.Empty(second.ChangeTracker.Entries());
        Assert.Equal(defaultTimeout, second.Database.GetCommandTimeout());
    }

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

    [Fact]
    [CapabilityRequirement("external-connection")]
    [BehavioralCoverage("EF10-CONTEXT-DISPOSE-EXTERNAL-CONNECTION-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SetDbConnection", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.OpenConnection", "Microsoft.EntityFrameworkCore.DbContext.Dispose")]
    public async Task Dispose_LeavesCallerOwnedConnectionOpen()
    {
        ProviderTestRuntime.RequireCapability("external-connection");
        var id = await SeedPersonAsync("Caller-owned sync connection");
        var externalConnection = ProviderTestRuntime.CreateConnection();
        var context = ProviderTestRuntime.CreateContext();

        try
        {
            context.Database.SetDbConnection(externalConnection, contextOwnsConnection: false);
            context.Database.OpenConnection();
            Assert.Equal(id, context.People.Single(person => person.Id == id).Id);

            context.Dispose();

            Assert.Equal(ConnectionState.Open, externalConnection.State);
        }
        finally
        {
            context.Dispose();
            await externalConnection.DisposeAsync();
        }
    }

    [Fact]
    [CapabilityRequirement("external-connection")]
    [BehavioralCoverage("EF10-CONTEXT-DISPOSE-EXTERNAL-CONNECTION-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SetDbConnection", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.OpenConnectionAsync", "Microsoft.EntityFrameworkCore.DbContext.DisposeAsync")]
    public async Task DisposeAsync_LeavesCallerOwnedConnectionOpen()
    {
        ProviderTestRuntime.RequireCapability("external-connection");
        var id = await SeedPersonAsync("Caller-owned async connection");
        var externalConnection = ProviderTestRuntime.CreateConnection();
        var context = ProviderTestRuntime.CreateContext();

        try
        {
            context.Database.SetDbConnection(externalConnection, contextOwnsConnection: false);
            await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
            Assert.Equal(id, await context.People
                .Where(person => person.Id == id)
                .Select(person => person.Id)
                .SingleAsync(TestContext.Current.CancellationToken));

            await context.DisposeAsync();

            Assert.Equal(ConnectionState.Open, externalConnection.State);
        }
        finally
        {
            await context.DisposeAsync();
            await externalConnection.DisposeAsync();
        }
    }

    private static async Task<Guid> SeedPersonAsync(string name)
    {
        var person = new Person { Id = Guid.NewGuid(), Name = name, Age = 36 };
        await using var seed = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
        seed.People.Add(person);
        await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        return person.Id;
    }
}
