using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("transactions")]
[CapabilityRequirement("savepoints")]
public sealed class SavepointTests
{
    [Fact]
    [BehavioralCoverage("EF10-SAVEPOINT-ROLLBACK-ASYNC", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.CreateSavepointAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.RollbackToSavepointAsync")]
    public async Task RollbackToSavepointAsync_RevertsChangesAfterSavepoint()
    {
        ProviderTestRuntime.RequireCapability("savepoints");
        ProviderTestRuntime.RequireCapability("transactions");
        var id = Guid.NewGuid();
        var token = TestContext.Current.CancellationToken;

        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, token);
        await using var transaction = await context.Database.BeginTransactionAsync(token);
        await transaction.CreateSavepointAsync("before_insert", token);
        context.People.Add(new Person { Id = id, Name = "Savepoint rollback", Age = 33 });
        await context.SaveChangesAsync(token);
        await transaction.RollbackToSavepointAsync("before_insert", token);
        context.ChangeTracker.Clear();

        Assert.False(await context.People.AnyAsync(person => person.Id == id, token));
        await transaction.CommitAsync(token);
    }

    [Fact]
    [BehavioralCoverage("EF10-SAVEPOINT-ROLLBACK-SYNC", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.CreateSavepoint", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.RollbackToSavepoint")]
    public async Task RollbackToSavepoint_RevertsChangesAfterSavepointSynchronously()
    {
        ProviderTestRuntime.RequireCapability("savepoints");
        ProviderTestRuntime.RequireCapability("transactions");
        var id = Guid.NewGuid();
        var token = TestContext.Current.CancellationToken;

        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, token);
        using var transaction = context.Database.BeginTransaction();
        transaction.CreateSavepoint("before_insert");
        context.People.Add(new Person { Id = id, Name = "Savepoint rollback sync", Age = 34 });
        context.SaveChanges();
        transaction.RollbackToSavepoint("before_insert");
        context.ChangeTracker.Clear();

        Assert.False(context.People.Any(person => person.Id == id));
        transaction.Commit();
    }
}
