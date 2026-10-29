using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("filtered-indexes")]
public sealed class FilteredIndexTests
{
    [Fact]
    [BehavioralCoverage("EF10-FILTERED-INDEX-ASYNC", "Microsoft.EntityFrameworkCore.RelationalIndexBuilderExtensions.HasFilter", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.DbUpdateException")]
    public async Task FilteredUniqueIndex_ConstrainsOnlyRowsMatchingFilterAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("filtered-indexes");
        var value = $"filtered-{Guid.NewGuid():N}";
        var expected = new[]
        {
            new FilteredUniqueValueRecord { Id = Guid.NewGuid(), Value = value, IsActive = 0 },
            new FilteredUniqueValueRecord { Id = Guid.NewGuid(), Value = value, IsActive = 0 },
            new FilteredUniqueValueRecord { Id = Guid.NewGuid(), Value = value, IsActive = 1 }
        };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.Set<FilteredUniqueValueRecord>().AddRange(expected);
            Assert.Equal(3, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var duplicate = ProviderTestRuntime.CreateContext())
        {
            duplicate.Set<FilteredUniqueValueRecord>().Add(new FilteredUniqueValueRecord
            {
                Id = Guid.NewGuid(), Value = value, IsActive = 1
            });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => duplicate.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.Single(exception.Entries);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.Set<FilteredUniqueValueRecord>().AsNoTracking()
            .Where(record => record.Value == value)
            .OrderBy(record => record.IsActive)
            .ThenBy(record => record.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected.Length, actual.Count);
        Assert.Equal(2, actual.Count(record => record.IsActive == 0));
        Assert.Equal(1, actual.Count(record => record.IsActive == 1));
    }

    [Fact]
    [BehavioralCoverage("EF10-FILTERED-INDEX-SYNC", "Microsoft.EntityFrameworkCore.RelationalIndexBuilderExtensions.HasFilter", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.DbUpdateException")]
    public async Task FilteredUniqueIndex_ConstrainsOnlyRowsMatchingFilterSynchronously()
    {
        ProviderTestRuntime.RequireCapability("filtered-indexes");
        var value = $"filtered-sync-{Guid.NewGuid():N}";
        var expected = new[]
        {
            new FilteredUniqueValueRecord { Id = Guid.NewGuid(), Value = value, IsActive = 0 },
            new FilteredUniqueValueRecord { Id = Guid.NewGuid(), Value = value, IsActive = 0 },
            new FilteredUniqueValueRecord { Id = Guid.NewGuid(), Value = value, IsActive = 1 }
        };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.Set<FilteredUniqueValueRecord>().AddRange(expected);
            Assert.Equal(3, seed.SaveChanges());
        }

        await using (var duplicate = ProviderTestRuntime.CreateContext())
        {
            duplicate.Set<FilteredUniqueValueRecord>().Add(new FilteredUniqueValueRecord
            {
                Id = Guid.NewGuid(), Value = value, IsActive = 1
            });
            var exception = Assert.Throws<DbUpdateException>(() => duplicate.SaveChanges());
            Assert.Single(exception.Entries);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.Set<FilteredUniqueValueRecord>().AsNoTracking()
            .Where(record => record.Value == value)
            .OrderBy(record => record.IsActive)
            .ThenBy(record => record.Id)
            .ToList();
        Assert.Equal(expected.Length, actual.Count);
        Assert.Equal(2, actual.Count(record => record.IsActive == 0));
        Assert.Equal(1, actual.Count(record => record.IsActive == 1));
    }
}
