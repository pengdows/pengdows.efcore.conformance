using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class DatabaseFacadeTests
{
    [Fact]
    [BehavioralCoverage("EF10-DATABASE-FACADE-COMMAND-TIMEOUT", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.IsRelational", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetCommandTimeout", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SetCommandTimeout")]
    public async Task CommandTimeout_CanBeSetAndReadInSecondsOrTimeSpan()
    {
        ProviderTestRuntime.RequireCapability("relational");
        await using var context = ProviderTestRuntime.CreateContext();
        var initialTimeout = context.Database.GetCommandTimeout();

        Assert.True(context.Database.IsRelational());

        context.Database.SetCommandTimeout(23);
        Assert.Equal(23, context.Database.GetCommandTimeout());

        context.Database.SetCommandTimeout(TimeSpan.FromSeconds(7));
        Assert.Equal(7, context.Database.GetCommandTimeout());

        context.Database.SetCommandTimeout((int?)null);
        Assert.Equal(initialTimeout, context.Database.GetCommandTimeout());
    }

    [Fact]
    [BehavioralCoverage("EF10-DATABASE-FACADE-CONNECTION-STRING", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetConnectionString", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SetConnectionString")]
    public async Task ConnectionString_CanBeReadAndChangedBeforeOpeningConnection()
    {
        ProviderTestRuntime.RequireCapability("relational");
        await using var context = ProviderTestRuntime.CreateContext();
        var configured = context.Database.GetConnectionString();
        const string replacement = "Data Source=ef10-conformance-reconfigured.db";

        Assert.Equal(ProviderTestRuntime.ConnectionString, configured);

        context.Database.SetConnectionString(replacement);
        Assert.Equal(replacement, context.Database.GetConnectionString());

        context.Database.SetConnectionString(configured);
        Assert.Equal(configured, context.Database.GetConnectionString());
    }

    [Fact]
    [CapabilityRequirement("external-connection")]
    [BehavioralCoverage("EF10-DATABASE-FACADE-SET-CONNECTION", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SetDbConnection", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection")]
    public async Task SetDbConnection_UsesAnExternalConnectionToReadPersistedData()
    {
        ProviderTestRuntime.RequireCapability("external-connection");
        var id = Guid.NewGuid();
        const string expected = "External connection lookup";

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.People.Add(new Person { Id = id, Name = expected, Age = 42 });
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var externalConnection = ProviderTestRuntime.CreateConnection();
        await using var context = ProviderTestRuntime.CreateContext();
        context.Database.SetDbConnection(externalConnection, contextOwnsConnection: false);
        Assert.Same(externalConnection, context.Database.GetDbConnection());

        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        var actual = await context.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
        await context.Database.CloseConnectionAsync();

        Assert.Equal(expected, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-DATABASE-FACADE-PROVIDER-NAME", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.ProviderName")]
    public async Task ProviderName_IdentifiesConfiguredProvider()
    {
        ProviderTestRuntime.RequireCapability("relational");
        await using var context = ProviderTestRuntime.CreateContext();

        Assert.False(string.IsNullOrWhiteSpace(context.Database.ProviderName));
    }

    [Fact]
    [CapabilityRequirement("savepoints")]
    [BehavioralCoverage("EF10-DATABASE-FACADE-AUTO-SAVEPOINTS", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.AutoSavepointsEnabled")]
    public async Task AutoSavepointsEnabled_CanBeChangedAndRestored()
    {
        ProviderTestRuntime.RequireCapability("savepoints");
        await using var context = ProviderTestRuntime.CreateContext();
        var original = context.Database.AutoSavepointsEnabled;

        context.Database.AutoSavepointsEnabled = !original;
        Assert.Equal(!original, context.Database.AutoSavepointsEnabled);

        context.Database.AutoSavepointsEnabled = original;
        Assert.Equal(original, context.Database.AutoSavepointsEnabled);
    }

    [Fact]
    [BehavioralCoverage("EF10-DATABASE-FACADE-AUTO-TRANSACTION-BEHAVIOR", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.AutoTransactionBehavior", "Microsoft.EntityFrameworkCore.AutoTransactionBehavior", "Microsoft.EntityFrameworkCore.AutoTransactionBehavior.Always", "Microsoft.EntityFrameworkCore.AutoTransactionBehavior.Never", "Microsoft.EntityFrameworkCore.AutoTransactionBehavior.WhenNeeded")]
    public async Task AutoTransactionBehavior_CanBeChangedAndRestored()
    {
        ProviderTestRuntime.RequireCapability("relational");
        await using var context = ProviderTestRuntime.CreateContext();
        var original = context.Database.AutoTransactionBehavior;

        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Never;
        Assert.Equal(AutoTransactionBehavior.Never, context.Database.AutoTransactionBehavior);

        context.Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
        Assert.Equal(AutoTransactionBehavior.Always, context.Database.AutoTransactionBehavior);

        context.Database.AutoTransactionBehavior = original;
        Assert.Equal(original, context.Database.AutoTransactionBehavior);
    }

    [Fact]
    [BehavioralCoverage("EF10-DATABASE-FACADE-AUTO-TRANSACTIONS-LEGACY", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.AutoTransactionsEnabled", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.AutoTransactionBehavior", "Microsoft.EntityFrameworkCore.AutoTransactionBehavior", "Microsoft.EntityFrameworkCore.AutoTransactionBehavior.Never", "Microsoft.EntityFrameworkCore.AutoTransactionBehavior.WhenNeeded")]
    public async Task AutoTransactionsEnabled_ReflectsTheAutomaticTransactionBehavior()
    {
        ProviderTestRuntime.RequireCapability("relational");
        await using var context = ProviderTestRuntime.CreateContext();
#pragma warning disable CS0618
        var original = context.Database.AutoTransactionsEnabled;

        context.Database.AutoTransactionsEnabled = false;
        Assert.False(context.Database.AutoTransactionsEnabled);
        Assert.Equal(AutoTransactionBehavior.Never, context.Database.AutoTransactionBehavior);

        context.Database.AutoTransactionsEnabled = true;
        Assert.True(context.Database.AutoTransactionsEnabled);
        Assert.Equal(AutoTransactionBehavior.WhenNeeded, context.Database.AutoTransactionBehavior);

        context.Database.AutoTransactionsEnabled = original;
#pragma warning restore CS0618
    }
}
