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

    [Fact]
    [BehavioralCoverage("EF10-CONCURRENCY-RESOLVE-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.DbUpdateException.Entries", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.GetDatabaseValuesAsync", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues.SetValues")]
    public async Task SaveChangesAsync_ConflictCanBeResolvedAndRetried()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);
        await using var first = ProviderTestRuntime.CreateContext();
        await using var second = ProviderTestRuntime.CreateContext();
        var firstCopy = await first.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        var secondCopy = await second.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);

        firstCopy.Age = 61;
        firstCopy.Version++;
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);

        secondCopy.Age = 62;
        secondCopy.Version++;
        var conflict = await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => second.SaveChangesAsync(TestContext.Current.CancellationToken));
        var entry = Assert.Single(conflict.Entries);
        var databaseValues = await entry.GetDatabaseValuesAsync(TestContext.Current.CancellationToken);
        Assert.NotNull(databaseValues);
        Assert.Equal(61, databaseValues.GetValue<int>(nameof(Person.Age)));
        entry.OriginalValues.SetValues(databaseValues);
        secondCopy.Version++;

        Assert.Equal(1, await second.SaveChangesAsync(TestContext.Current.CancellationToken));

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = await verify.People.AsNoTracking().SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(62, persisted.Age);
        Assert.Equal(2, persisted.Version);
    }

    [Fact]
    [BehavioralCoverage("EF10-CONCURRENCY-RESOLVE-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.DbUpdateException.Entries", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.GetDatabaseValues", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues.SetValues")]
    public async Task SaveChanges_ConflictCanBeResolvedAndRetriedSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);
        await using var first = ProviderTestRuntime.CreateContext();
        await using var second = ProviderTestRuntime.CreateContext();
        var firstCopy = first.People.Single(item => item.Id == id);
        var secondCopy = second.People.Single(item => item.Id == id);

        firstCopy.Age = 71;
        firstCopy.Version++;
        first.SaveChanges();

        secondCopy.Age = 72;
        secondCopy.Version++;
        var conflict = Assert.Throws<DbUpdateConcurrencyException>(() => second.SaveChanges());
        var entry = Assert.Single(conflict.Entries);
        var databaseValues = entry.GetDatabaseValues();
        Assert.NotNull(databaseValues);
        Assert.Equal(71, databaseValues.GetValue<int>(nameof(Person.Age)));
        entry.OriginalValues.SetValues(databaseValues);
        secondCopy.Version++;

        Assert.Equal(1, second.SaveChanges());

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = verify.People.AsNoTracking().Single(item => item.Id == id);
        Assert.Equal(72, persisted.Age);
        Assert.Equal(2, persisted.Version);
    }

    private static async Task SeedAsync(Guid id)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = $"Concurrent {id:N}", Age = 60 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
