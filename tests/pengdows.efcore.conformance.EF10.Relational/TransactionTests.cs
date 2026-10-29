using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("transactions")]
public sealed class TransactionTests
{
    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-ROLLBACK-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.BeginTransactionAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.RollbackAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task TransactionRollback_DoesNotPersistSavedChanges()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var id = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            context.People.Add(new Person { Id = id, Name = "Rolled back", Age = 18 });
            Assert.Equal(1, await context.SaveChangesAsync(cancellationToken));
            await transaction.RollbackAsync(cancellationToken);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(person => person.Id == id, cancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-COMMIT-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.BeginTransaction", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.Commit", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task TransactionCommit_PersistsSavedChangesSynchronously()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var id = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, cancellationToken);
            using var transaction = context.Database.BeginTransaction();
            context.People.Add(new Person { Id = id, Name = "Committed", Age = 19 });
            Assert.Equal(1, context.SaveChanges());
            transaction.Commit();
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, cancellationToken);
        Assert.Equal("Committed", persisted.Name);
        Assert.Equal(19, persisted.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-ROLLBACK-SYNC", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.BeginTransaction", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.Rollback", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task TransactionRollback_DoesNotPersistSavedChangesSynchronously()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var id = Guid.NewGuid();

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
            using var transaction = context.Database.BeginTransaction();
            context.People.Add(new Person { Id = id, Name = "Rolled back sync", Age = 20 });
            Assert.Equal(1, context.SaveChanges());
            transaction.Rollback();
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(person => person.Id == id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-COMMIT-ASYNC", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.BeginTransactionAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.CommitAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task TransactionCommit_PersistsSavedChangesAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var id = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            context.People.Add(new Person { Id = id, Name = "Committed async", Age = 21 });
            Assert.Equal(1, await context.SaveChangesAsync(cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, cancellationToken);
        Assert.Equal("Committed async", persisted.Name);
        Assert.Equal(21, persisted.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-CURRENT-SYNC", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CurrentTransaction", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.BeginTransaction")]
    public async Task CurrentTransaction_ReflectsSynchronousTransactionLifetime()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.Null(context.Database.CurrentTransaction);
        using (context.Database.BeginTransaction())
        {
            Assert.NotNull(context.Database.CurrentTransaction);
        }

        Assert.Null(context.Database.CurrentTransaction);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-CURRENT-ASYNC", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CurrentTransaction", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.BeginTransactionAsync")]
    public async Task CurrentTransaction_ReflectsAsynchronousTransactionLifetime()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.Null(context.Database.CurrentTransaction);
        await using (await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            Assert.NotNull(context.Database.CurrentTransaction);
        }

        Assert.Null(context.Database.CurrentTransaction);
    }
}
