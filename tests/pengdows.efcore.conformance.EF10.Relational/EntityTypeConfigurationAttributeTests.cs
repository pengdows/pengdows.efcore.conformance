using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class EntityTypeConfigurationAttributeTests
{
    [Fact]
    [BehavioralCoverage("EF10-ENTITY-TYPE-CONFIGURATION-ASYNC", "Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute", "Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute..ctor", "Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute.EntityTypeConfigurationType", "Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1", "Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1.Configure", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task EntityTypeConfigurationAttribute_AppliesMappingsForAsyncPersistence()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var expected = new AttributedConfigurationRecord
        {
            Id = Guid.NewGuid(),
            Value = "Configured by the entity type attribute"
        };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.AttributedConfigurationRecords.Add(expected);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.AttributedConfigurationRecords.AsNoTracking()
            .SingleAsync(record => record.Id == expected.Id, TestContext.Current.CancellationToken);
        Assert.Equal(expected.Value, actual.Value);
    }

    [Fact]
    [BehavioralCoverage("EF10-ENTITY-TYPE-CONFIGURATION-SYNC", "Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute", "Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute..ctor", "Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute.EntityTypeConfigurationType", "Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1", "Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1.Configure", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task EntityTypeConfigurationAttribute_AppliesMappingsForSyncPersistence()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var expected = new AttributedConfigurationRecord
        {
            Id = Guid.NewGuid(),
            Value = "Configured synchronously by the entity type attribute"
        };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.AttributedConfigurationRecords.Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.AttributedConfigurationRecords.AsNoTracking()
            .Single(record => record.Id == expected.Id);
        Assert.Equal(expected.Value, actual.Value);
    }
}
