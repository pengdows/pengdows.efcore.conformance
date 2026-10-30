using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class BulkOperationTests
{
    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync")]
    public async Task ExecuteUpdateAsync_ChangesMatchingRows()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var id = await SeedAsync();

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = await update.People.SingleAsync(
                person => person.Id == id,
                TestContext.Current.CancellationToken);
            var affected = await update.People.Where(person => person.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(person => person.Age, person => person.Age + 1),
                    TestContext.Current.CancellationToken);
            Assert.Equal(1, affected);
            Assert.Equal(40, tracked.Age);
            Assert.Equal(EntityState.Unchanged, update.Entry(tracked).State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(41, actual.Age);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdate")]
    public async Task ExecuteUpdate_ChangesMatchingRowsSynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var id = await SeedAsync();

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = update.People.Single(person => person.Id == id);
            var affected = update.People.Where(person => person.Id == id)
                .ExecuteUpdate(setters => setters.SetProperty(person => person.Age, person => person.Age + 1));
            Assert.Equal(1, affected);
            Assert.Equal(40, tracked.Age);
            Assert.Equal(EntityState.Unchanged, update.Entry(tracked).State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.People.AsNoTracking().Single(person => person.Id == id);
        Assert.Equal(41, actual.Age);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-MULTIPLE-PROPERTIES-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync")]
    public async Task ExecuteUpdateAsync_ChangesMultipleProperties()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var id = await SeedAsync();

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var affected = await update.People.Where(person => person.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(person => person.Name, person => person.Name + " updated")
                        .SetProperty(person => person.Age, person => person.Age + 2),
                    TestContext.Current.CancellationToken);
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal($"Bulk {id:N} updated", actual.Name);
        Assert.Equal(42, actual.Age);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-NO-MATCH-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync")]
    public async Task ExecuteUpdateAsync_ReturnsZeroWhenNoRowsMatch()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var missingId = Guid.NewGuid();
        await using var context = ProviderTestRuntime.CreateContext();

        var affected = await context.People
            .Where(person => person.Id == missingId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(person => person.Age, 50),
                TestContext.Current.CancellationToken);

        Assert.Equal(0, affected);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-MULTIPLE-PROPERTIES-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdate")]
    public async Task ExecuteUpdate_ChangesMultiplePropertiesSynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var id = await SeedAsync();

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var affected = update.People.Where(person => person.Id == id)
                .ExecuteUpdate(
                    setters => setters
                        .SetProperty(person => person.Name, person => person.Name + " sync updated")
                        .SetProperty(person => person.Age, person => person.Age + 3));
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.People.AsNoTracking().Single(person => person.Id == id);
        Assert.Equal($"Bulk {id:N} sync updated", actual.Name);
        Assert.Equal(43, actual.Age);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-NO-MATCH-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdate")]
    public async Task ExecuteUpdate_ReturnsZeroWhenNoRowsMatchSynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var missingId = Guid.NewGuid();
        await using var context = ProviderTestRuntime.CreateContext();

        var affected = context.People
            .Where(person => person.Id == missingId)
            .ExecuteUpdate(setters => setters.SetProperty(person => person.Age, 50));

        Assert.Equal(0, affected);
    }

    [Fact]
    [CapabilityRequirement("execute-delete")]
    [BehavioralCoverage("EF10-BULK-DELETE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDeleteAsync")]
    public async Task ExecuteDeleteAsync_RemovesMatchingRows()
    {
        ProviderTestRuntime.RequireCapability("execute-delete");
        var id = await SeedAsync();

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var tracked = await delete.People.SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);
            Assert.Equal(1, await delete.People.Where(person => person.Id == id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
            Assert.Equal(EntityState.Unchanged, delete.Entry(tracked).State);
            Assert.Equal(40, tracked.Age);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(person => person.Id == id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("execute-delete")]
    [BehavioralCoverage("EF10-BULK-DELETE-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDelete")]
    public async Task ExecuteDelete_RemovesMatchingRowsSynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-delete");
        var id = await SeedAsync();

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var tracked = delete.People.Single(person => person.Id == id);
            Assert.Equal(1, delete.People.Where(person => person.Id == id).ExecuteDelete());
            Assert.Equal(EntityState.Unchanged, delete.Entry(tracked).State);
            Assert.Equal(40, tracked.Age);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(verify.People.Any(person => person.Id == id));
    }

    [Fact]
    [CapabilityRequirement("execute-delete")]
    [BehavioralCoverage("EF10-BULK-DELETE-NO-MATCH-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDeleteAsync")]
    public async Task ExecuteDeleteAsync_ReturnsZeroWhenNoRowsMatch()
    {
        ProviderTestRuntime.RequireCapability("execute-delete");
        var missingId = Guid.NewGuid();
        await using var context = ProviderTestRuntime.CreateContext();

        var affected = await context.People
            .Where(person => person.Id == missingId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, affected);
    }

    [Fact]
    [CapabilityRequirement("execute-delete")]
    [BehavioralCoverage("EF10-BULK-DELETE-NO-MATCH-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDelete")]
    public async Task ExecuteDelete_ReturnsZeroWhenNoRowsMatchSynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-delete");
        var missingId = Guid.NewGuid();
        await using var context = ProviderTestRuntime.CreateContext();

        var affected = context.People.Where(person => person.Id == missingId).ExecuteDelete();

        Assert.Equal(0, affected);
    }

    private static async Task<Guid> SeedAsync()
    {
        var id = Guid.NewGuid();
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = $"Bulk {id:N}", Age = 40 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return id;
    }
}
