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

    [Fact]
    [BehavioralCoverage("EF10-JSON-EXECUTE-UPDATE-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "Microsoft.EntityFrameworkCore.RelationalComplexPropertyBuilderExtensions.ToJson", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync")]
    public async Task ExecuteUpdateAsync_UpdatesNestedJsonProperty()
    {
        ProviderTestRuntime.RequireCapability("json-mapping");
        var expected = CreateExecuteUpdateRecord("JSON bulk update source", 10);
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.JsonExecuteUpdateRecords.Add(expected);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var affected = await update.JsonExecuteUpdateRecords
                .Where(record => record.Id == expected.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(record => record.Details.Views, record => record.Details.Views + 1),
                    TestContext.Current.CancellationToken);
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.JsonExecuteUpdateRecords.AsNoTracking()
            .SingleAsync(record => record.Id == expected.Id, TestContext.Current.CancellationToken);
        Assert.Equal("JSON bulk update source", actual.Details.Title);
        Assert.Equal(11, actual.Details.Views);
    }

    [Fact]
    [BehavioralCoverage("EF10-JSON-EXECUTE-UPDATE-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.ComplexProperty", "Microsoft.EntityFrameworkCore.RelationalComplexPropertyBuilderExtensions.ToJson", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdate")]
    public async Task ExecuteUpdate_UpdatesNestedJsonPropertySynchronously()
    {
        ProviderTestRuntime.RequireCapability("json-mapping");
        var expected = CreateExecuteUpdateRecord("JSON bulk update sync source", 20);
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.JsonExecuteUpdateRecords.Add(expected);
            await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        const int updatedViews = 27;
        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var affected = update.JsonExecuteUpdateRecords
                .Where(record => record.Id == expected.Id)
                .ExecuteUpdate(setters => setters.SetProperty(record => record.Details.Views, updatedViews));
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.JsonExecuteUpdateRecords.AsNoTracking()
            .SingleAsync(record => record.Id == expected.Id, TestContext.Current.CancellationToken);
        Assert.Equal("JSON bulk update sync source", actual.Details.Title);
        Assert.Equal(updatedViews, actual.Details.Views);
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

    private static JsonExecuteUpdateRecord CreateExecuteUpdateRecord(string title, int views) => new()
    {
        Id = Guid.NewGuid(),
        Details = new JsonExecuteUpdateDetails { Title = title, Views = views }
    };
}
