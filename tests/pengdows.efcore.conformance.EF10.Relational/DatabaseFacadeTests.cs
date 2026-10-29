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
}
