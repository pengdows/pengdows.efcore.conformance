using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class ConcurrencyTests
{
    [Fact]
    [BehavioralCoverage("EF10-CONCURRENCY-SAVE-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException")]
    public async Task SaveChangesAsync_ReportsOptimisticConcurrencyConflict()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);
        await using var first = ProviderTestRuntime.CreateContext();
        await using var second = ProviderTestRuntime.CreateContext();
        var firstCopy = await first.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        var secondCopy = await second.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);

        firstCopy.Age = 61;
        firstCopy.Version++;
        Assert.Equal(1, await first.SaveChangesAsync(TestContext.Current.CancellationToken));

        secondCopy.Age = 62;
        secondCopy.Version++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.SaveChangesAsync(TestContext.Current.CancellationToken));

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = await verify.People.AsNoTracking().SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(61, persisted.Age);
        Assert.Equal(1, persisted.Version);
    }

    [Fact]
    [BehavioralCoverage("EF10-CONCURRENCY-SAVE-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException")]
    public async Task SaveChanges_ReportsOptimisticConcurrencyConflictSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);
        await using var first = ProviderTestRuntime.CreateContext();
        await using var second = ProviderTestRuntime.CreateContext();
        var firstCopy = await first.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        var secondCopy = await second.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);

        firstCopy.Age = 71;
        firstCopy.Version++;
        Assert.Equal(1, first.SaveChanges());

        secondCopy.Age = 72;
        secondCopy.Version++;
        Assert.Throws<DbUpdateConcurrencyException>(() => second.SaveChanges());

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = await verify.People.AsNoTracking().SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(71, persisted.Age);
        Assert.Equal(1, persisted.Version);
    }

    private static async Task SeedAsync(Guid id)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = $"Concurrent {id:N}", Age = 60 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
