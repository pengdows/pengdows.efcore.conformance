using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("json-mapping")]
public sealed class JsonMappingTests
{
    [Fact]
    [BehavioralCoverage("EF10-JSON-MAPPING-ASYNC", "Microsoft.EntityFrameworkCore.RelationalOwnedNavigationBuilderExtensions.ToJson", "Microsoft.EntityFrameworkCore.Metadata.Builders.OwnedNavigationBuilder.OwnsOne", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync", "System.Linq.Queryable.Where")]
    [NativeTypeCoverage("JSONB", typeof(JsonProfile), "EF10-JSON-MAPPING-ASYNC", "Npgsql EF Core PostgreSQL")]
    public async Task OwnedToJson_PersistsAndQueriesNestedValuesAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("json-mapping");
        var expected = CreateRecord("JSON async profile", true);
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.JsonProfileRecords.Add(expected);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.JsonProfileRecords.AsNoTracking()
            .Where(record => record.Id == expected.Id &&
                             record.Profile.Biography == expected.Profile.Biography &&
                             record.Profile.Preferences.EmailNotifications)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected.Profile.Biography, actual.Profile.Biography);
        Assert.True(actual.Profile.Preferences.EmailNotifications);
    }

    [Fact]
    [BehavioralCoverage("EF10-JSON-MAPPING-SYNC", "Microsoft.EntityFrameworkCore.RelationalOwnedNavigationBuilderExtensions.ToJson", "Microsoft.EntityFrameworkCore.Metadata.Builders.OwnedNavigationBuilder.OwnsOne", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single", "System.Linq.Queryable.Where")]
    [NativeTypeCoverage("JSONB", typeof(JsonProfile), "EF10-JSON-MAPPING-SYNC", "Npgsql EF Core PostgreSQL")]
    public async Task OwnedToJson_PersistsAndQueriesNestedValuesSynchronously()
    {
        ProviderTestRuntime.RequireCapability("json-mapping");
        var expected = CreateRecord("JSON sync profile", false);
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.JsonProfileRecords.Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.JsonProfileRecords.AsNoTracking()
            .Where(record => record.Id == expected.Id &&
                             record.Profile.Biography == expected.Profile.Biography &&
                             !record.Profile.Preferences.EmailNotifications)
            .Single();
        Assert.Equal(expected.Profile.Biography, actual.Profile.Biography);
        Assert.False(actual.Profile.Preferences.EmailNotifications);
    }

    private static JsonProfileRecord CreateRecord(string biography, bool emailNotifications) => new()
    {
        Id = Guid.NewGuid(),
        Profile = new JsonProfile
        {
            Biography = biography,
            Preferences = new JsonPreferences { EmailNotifications = emailNotifications }
        }
    };
}
