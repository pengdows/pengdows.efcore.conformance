using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class ModelAttributeTests
{
    [Fact]
    [BehavioralCoverage("EF10-MODEL-ATTRIBUTES-ASYNC", "Microsoft.EntityFrameworkCore.PrimaryKeyAttribute", "Microsoft.EntityFrameworkCore.PrimaryKeyAttribute..ctor", "Microsoft.EntityFrameworkCore.PrimaryKeyAttribute.PropertyNames", "Microsoft.EntityFrameworkCore.UnicodeAttribute", "Microsoft.EntityFrameworkCore.UnicodeAttribute..ctor", "Microsoft.EntityFrameworkCore.UnicodeAttribute.IsUnicode", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task MappingAttributes_ConfigureKeyAndColumnForAsyncPersistence()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var expected = new AttributeConfiguredRecord
        {
            Key = Guid.NewGuid(),
            DisplayName = "Attribute configured row"
        };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.AttributeConfiguredRecords.Add(expected);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.AttributeConfiguredRecords.AsNoTracking()
            .SingleAsync(record => record.Key == expected.Key, TestContext.Current.CancellationToken);
        Assert.Equal(expected.DisplayName, actual.DisplayName);
    }

    [Fact]
    [BehavioralCoverage("EF10-MODEL-ATTRIBUTES-SYNC", "Microsoft.EntityFrameworkCore.PrimaryKeyAttribute", "Microsoft.EntityFrameworkCore.PrimaryKeyAttribute..ctor", "Microsoft.EntityFrameworkCore.PrimaryKeyAttribute.PropertyNames", "Microsoft.EntityFrameworkCore.UnicodeAttribute", "Microsoft.EntityFrameworkCore.UnicodeAttribute..ctor", "Microsoft.EntityFrameworkCore.UnicodeAttribute.IsUnicode", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task MappingAttributes_ConfigureKeyAndColumnForSyncPersistence()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var expected = new AttributeConfiguredRecord
        {
            Key = Guid.NewGuid(),
            DisplayName = "Attribute configured sync row"
        };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.AttributeConfiguredRecords.Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.AttributeConfiguredRecords.AsNoTracking()
            .Single(record => record.Key == expected.Key);
        Assert.Equal(expected.DisplayName, actual.DisplayName);
    }
}
