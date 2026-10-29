using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class PropertyAccessModeTests
{
    [Fact]
    [BehavioralCoverage("EF10-PROPERTY-ACCESS-MODES-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync", "Microsoft.EntityFrameworkCore.PropertyAccessMode", "Microsoft.EntityFrameworkCore.PropertyAccessMode.Field", "Microsoft.EntityFrameworkCore.PropertyAccessMode.FieldDuringConstruction", "Microsoft.EntityFrameworkCore.PropertyAccessMode.Property", "Microsoft.EntityFrameworkCore.PropertyAccessMode.PreferField", "Microsoft.EntityFrameworkCore.PropertyAccessMode.PreferFieldDuringConstruction", "Microsoft.EntityFrameworkCore.PropertyAccessMode.PreferProperty")]
    public async Task PropertyAccessModes_ApplyConfiguredAccessDuringAsyncMaterialization()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var record = CreateRecord();

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.PropertyAccessRecords.Add(record);
            Assert.Equal(1, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = await query.PropertyAccessRecords.AsNoTracking()
            .SingleAsync(item => item.Id == record.Id, TestContext.Current.CancellationToken);

        AssertValues(actual);
        Assert.Equal(2, actual.SetterCalls);
    }

    [Fact]
    [BehavioralCoverage("EF10-PROPERTY-ACCESS-MODES-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single", "Microsoft.EntityFrameworkCore.PropertyAccessMode", "Microsoft.EntityFrameworkCore.PropertyAccessMode.Field", "Microsoft.EntityFrameworkCore.PropertyAccessMode.FieldDuringConstruction", "Microsoft.EntityFrameworkCore.PropertyAccessMode.Property", "Microsoft.EntityFrameworkCore.PropertyAccessMode.PreferField", "Microsoft.EntityFrameworkCore.PropertyAccessMode.PreferFieldDuringConstruction", "Microsoft.EntityFrameworkCore.PropertyAccessMode.PreferProperty")]
    public async Task PropertyAccessModes_ApplyConfiguredAccessDuringSyncMaterialization()
    {
        var record = CreateRecord();

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.PropertyAccessRecords.Add(record);
            Assert.Equal(1, seed.SaveChanges());
        }

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = query.PropertyAccessRecords.AsNoTracking().Single(item => item.Id == record.Id);

        AssertValues(actual);
        Assert.Equal(2, actual.SetterCalls);
    }

    private static PropertyAccessRecord CreateRecord() => new()
    {
        Id = Guid.NewGuid(),
        Field = "field",
        FieldDuringConstruction = "field during construction",
        Property = "property",
        PreferField = "prefer field",
        PreferFieldDuringConstruction = "prefer field during construction",
        PreferProperty = "prefer property"
    };

    private static void AssertValues(PropertyAccessRecord actual)
    {
        Assert.Equal("field", actual.Field);
        Assert.Equal("field during construction", actual.FieldDuringConstruction);
        Assert.Equal("property", actual.Property);
        Assert.Equal("prefer field", actual.PreferField);
        Assert.Equal("prefer field during construction", actual.PreferFieldDuringConstruction);
        Assert.Equal("prefer property", actual.PreferProperty);
    }
}
