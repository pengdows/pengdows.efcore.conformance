using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class BackingFieldAttributeTests
{
    [Fact]
    [BehavioralCoverage("EF10-BACKING-FIELD-ASYNC", "Microsoft.EntityFrameworkCore.BackingFieldAttribute", "Microsoft.EntityFrameworkCore.BackingFieldAttribute..ctor", "Microsoft.EntityFrameworkCore.BackingFieldAttribute.Name", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task BackingFieldAttribute_PersistsGetterOnlyPropertyUpdatesAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var record = new BackingFieldRecord { Id = Guid.NewGuid() };
        record.ChangeLabel("initial async label");

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.BackingFieldRecords.Add(record);
            Assert.Equal(1, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = await update.BackingFieldRecords.SingleAsync(
                item => item.Id == record.Id, TestContext.Current.CancellationToken);
            tracked.ChangeLabel("updated async label");
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.BackingFieldRecords.AsNoTracking()
            .SingleAsync(item => item.Id == record.Id, TestContext.Current.CancellationToken);
        Assert.Equal("updated async label", actual.Label);
    }

    [Fact]
    [BehavioralCoverage("EF10-BACKING-FIELD-SYNC", "Microsoft.EntityFrameworkCore.BackingFieldAttribute", "Microsoft.EntityFrameworkCore.BackingFieldAttribute..ctor", "Microsoft.EntityFrameworkCore.BackingFieldAttribute.Name", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task BackingFieldAttribute_PersistsGetterOnlyPropertyUpdatesSynchronously()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var record = new BackingFieldRecord { Id = Guid.NewGuid() };
        record.ChangeLabel("initial sync label");

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.BackingFieldRecords.Add(record);
            Assert.Equal(1, seed.SaveChanges());
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = update.BackingFieldRecords.Single(item => item.Id == record.Id);
            tracked.ChangeLabel("updated sync label");
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.BackingFieldRecords.AsNoTracking().Single(item => item.Id == record.Id);
        Assert.Equal("updated sync label", actual.Label);
    }
}
