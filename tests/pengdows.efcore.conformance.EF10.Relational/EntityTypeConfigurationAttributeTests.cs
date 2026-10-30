using Microsoft.EntityFrameworkCore;
using System.Data.Common;
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

    [Fact]
    [CapabilityRequirement("raw-sql")]
    [BehavioralCoverage("EF10-ENTITY-TYPE-CONFIGURATION-GENERIC-ASYNC", "Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute`2", "Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute`2..ctor", "Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1.Configure", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection")]
    public async Task GenericEntityTypeConfigurationAttribute_AppliesConfiguredTableAndColumnAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("raw-sql");
        var expected = new GenericAttributedConfigurationRecord
        {
            Id = Guid.NewGuid(),
            Value = $"Generic async configuration {Guid.NewGuid():N}"
        };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.GenericAttributedConfigurationRecords.Add(expected);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        await verify.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        using var command = CreateMappedValueCommand(verify, expected.Id);
        using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.Equal(expected.Value, reader.GetString(0));
        Assert.False(await reader.ReadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("raw-sql")]
    [BehavioralCoverage("EF10-ENTITY-TYPE-CONFIGURATION-GENERIC-SYNC", "Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute`2", "Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute`2..ctor", "Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1.Configure", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection")]
    public async Task GenericEntityTypeConfigurationAttribute_AppliesConfiguredTableAndColumnSynchronously()
    {
        ProviderTestRuntime.RequireCapability("raw-sql");
        var expected = new GenericAttributedConfigurationRecord
        {
            Id = Guid.NewGuid(),
            Value = $"Generic sync configuration {Guid.NewGuid():N}"
        };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.GenericAttributedConfigurationRecords.Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        verify.Database.OpenConnection();
        using var command = CreateMappedValueCommand(verify, expected.Id);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        Assert.Equal(expected.Value, reader.GetString(0));
        Assert.False(reader.Read());
    }

    private static DbCommand CreateMappedValueCommand(ConformanceDbContext context, Guid id)
    {
        var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT configured_value FROM ef10conformancegenericattributed WHERE configured_id = @id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = id;
        command.Parameters.Add(parameter);
        return command;
    }
}
