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

    [Fact]
    [BehavioralCoverage("EF10-SAVEPOINT-RELEASE-ASYNC", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.CreateSavepointAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.ReleaseSavepointAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.TransactionId", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.SupportsSavepoints")]
    public async Task ReleaseSavepointAsync_RemovesNamedSavepointAndAllowsCommit()
    {
        ProviderTestRuntime.RequireCapability("savepoints");
        var id = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            Assert.NotEqual(Guid.Empty, transaction.TransactionId);
            Assert.True(transaction.SupportsSavepoints);
            await transaction.CreateSavepointAsync("release_after_setup", cancellationToken);
            await transaction.ReleaseSavepointAsync("release_after_setup", cancellationToken);
            context.People.Add(new Person { Id = id, Name = "Savepoint released async", Age = 35 });
            Assert.Equal(1, await context.SaveChangesAsync(cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AnyAsync(person => person.Id == id, cancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-SAVEPOINT-RELEASE-SYNC", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.CreateSavepoint", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.ReleaseSavepoint", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.TransactionId", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.SupportsSavepoints")]
    public async Task ReleaseSavepoint_RemovesNamedSavepointAndAllowsCommitSynchronously()
    {
        ProviderTestRuntime.RequireCapability("savepoints");
        var id = Guid.NewGuid();

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
            using var transaction = context.Database.BeginTransaction();
            Assert.NotEqual(Guid.Empty, transaction.TransactionId);
            Assert.True(transaction.SupportsSavepoints);
            transaction.CreateSavepoint("release_after_setup");
            transaction.ReleaseSavepoint("release_after_setup");
            context.People.Add(new Person { Id = id, Name = "Savepoint released sync", Age = 36 });
            Assert.Equal(1, context.SaveChanges());
            transaction.Commit();
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AnyAsync(person => person.Id == id, TestContext.Current.CancellationToken));
    }
}
