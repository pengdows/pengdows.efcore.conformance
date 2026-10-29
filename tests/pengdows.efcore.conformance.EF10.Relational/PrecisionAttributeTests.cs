using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class PrecisionAttributeTests
{
    [Fact]
    [BehavioralCoverage("EF10-TYPES-PRECISION-ATTRIBUTE-ASYNC", "Microsoft.EntityFrameworkCore.PrecisionAttribute", "Microsoft.EntityFrameworkCore.PrecisionAttribute..ctor", "Microsoft.EntityFrameworkCore.PrecisionAttribute.Precision", "Microsoft.EntityFrameworkCore.PrecisionAttribute.Scale", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task PrecisionAttribute_RoundTripsConfiguredDecimalScaleAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var expected = new PrecisionAttributeRecord { Id = Guid.NewGuid(), Amount = 123456789012.3456m };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.Set<PrecisionAttributeRecord>().Add(expected);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.Set<PrecisionAttributeRecord>().AsNoTracking()
            .SingleAsync(record => record.Id == expected.Id, TestContext.Current.CancellationToken);
        Assert.Equal(expected.Amount, actual.Amount);
    }

    [Fact]
    [BehavioralCoverage("EF10-TYPES-PRECISION-ATTRIBUTE-SYNC", "Microsoft.EntityFrameworkCore.PrecisionAttribute", "Microsoft.EntityFrameworkCore.PrecisionAttribute..ctor", "Microsoft.EntityFrameworkCore.PrecisionAttribute.Precision", "Microsoft.EntityFrameworkCore.PrecisionAttribute.Scale", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task PrecisionAttribute_RoundTripsConfiguredDecimalScaleSynchronously()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var expected = new PrecisionAttributeRecord { Id = Guid.NewGuid(), Amount = -98765432109.8765m };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.Set<PrecisionAttributeRecord>().Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.Set<PrecisionAttributeRecord>().AsNoTracking()
            .Single(record => record.Id == expected.Id);
        Assert.Equal(expected.Amount, actual.Amount);
    }
}
