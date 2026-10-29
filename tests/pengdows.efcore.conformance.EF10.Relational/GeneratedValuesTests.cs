using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class GeneratedValuesTests
{
    [Fact]
    [BehavioralCoverage("EF10-GENERATED-KEYS-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder.ValueGeneratedOnAdd", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task StoreGeneratedIntegerKeys_AreAssignedAndPersistedAsynchronously()
    {
        var records = new[]
        {
            new GeneratedKeyRecord { Name = $"generated async one {Guid.NewGuid():N}" },
            new GeneratedKeyRecord { Name = $"generated async two {Guid.NewGuid():N}" }
        };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.GeneratedKeyRecords.AddRange(records);
            Assert.Equal(2, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.All(records, record => Assert.True(record.Id > 0));
            Assert.NotEqual(records[0].Id, records[1].Id);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var stored = await verify.GeneratedKeyRecords.AsNoTracking()
            .Where(record => records.Select(item => item.Id).Contains(record.Id))
            .OrderBy(record => record.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(records.Select(record => record.Name).Order(), stored.Select(record => record.Name).Order());
    }

    [Fact]
    [BehavioralCoverage("EF10-GENERATED-KEYS-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder.ValueGeneratedOnAdd", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task StoreGeneratedIntegerKeys_AreAssignedAndPersistedSynchronously()
    {
        var records = new[]
        {
            new GeneratedKeyRecord { Name = $"generated sync one {Guid.NewGuid():N}" },
            new GeneratedKeyRecord { Name = $"generated sync two {Guid.NewGuid():N}" }
        };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.GeneratedKeyRecords.AddRange(records);
            Assert.Equal(2, write.SaveChanges());
            Assert.All(records, record => Assert.True(record.Id > 0));
            Assert.NotEqual(records[0].Id, records[1].Id);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var stored = verify.GeneratedKeyRecords.AsNoTracking()
            .Where(record => records.Select(item => item.Id).Contains(record.Id))
            .OrderBy(record => record.Id)
            .ToList();
        Assert.Equal(records.Select(record => record.Name).Order(), stored.Select(record => record.Name).Order());
    }
}
