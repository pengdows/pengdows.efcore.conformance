using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
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
    [BehavioralCoverage("EF10-TRANSACTION-DISPOSE-ROLLBACK-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.BeginTransaction", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.Dispose", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task DisposingUncommittedTransaction_RollsBackSynchronously()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var id = Guid.NewGuid();

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
            var transaction = context.Database.BeginTransaction();
            context.People.Add(new Person { Id = id, Name = "Disposed transaction", Age = 22 });
            Assert.Equal(1, context.SaveChanges());
            transaction.Dispose();
            Assert.Null(context.Database.CurrentTransaction);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(person => person.Id == id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-DISPOSE-ROLLBACK-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.BeginTransactionAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.DisposeAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task DisposingUncommittedTransaction_RollsBackAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var id = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, cancellationToken);
            var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            context.People.Add(new Person { Id = id, Name = "Disposed async transaction", Age = 23 });
            Assert.Equal(1, await context.SaveChangesAsync(cancellationToken));
            await transaction.DisposeAsync();
            Assert.Null(context.Database.CurrentTransaction);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(person => person.Id == id, cancellationToken));
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
    [BehavioralCoverage("EF10-TRANSACTION-ISOLATION-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.BeginTransaction", "System.Data.Common.DbTransaction.IsolationLevel", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task BeginTransaction_UsesRequestedSerializableIsolationSynchronously()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var id = Guid.NewGuid();

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
            if (!ProviderTestRuntime.SupportsIsolation(IsolationLevel.Serializable))
            {
                var refused = Assert.Throws<NotSupportedException>(() => context.Database.BeginTransaction(IsolationLevel.Serializable));
                Assert.Contains("Serializable", refused.Message, StringComparison.Ordinal);
                return;
            }

            using var transaction = context.Database.BeginTransaction(IsolationLevel.Serializable);
            Assert.Equal(IsolationLevel.Serializable, transaction.GetDbTransaction().IsolationLevel);
            context.People.Add(new Person { Id = id, Name = "Serializable transaction", Age = 24 });
            Assert.Equal(1, context.SaveChanges());
            transaction.Commit();
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("Serializable transaction", await verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-ISOLATION-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.BeginTransactionAsync", "System.Data.Common.DbTransaction.IsolationLevel", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task BeginTransactionAsync_UsesRequestedSerializableIsolation()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var id = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, cancellationToken);
            if (!ProviderTestRuntime.SupportsIsolation(IsolationLevel.Serializable))
            {
                var refused = await Assert.ThrowsAsync<NotSupportedException>(() =>
                    context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken));
                Assert.Contains("Serializable", refused.Message, StringComparison.Ordinal);
                return;
            }

            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            Assert.Equal(IsolationLevel.Serializable, transaction.GetDbTransaction().IsolationLevel);
            context.People.Add(new Person { Id = id, Name = "Serializable async transaction", Age = 25 });
            Assert.Equal(1, await context.SaveChangesAsync(cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("Serializable async transaction", await verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .SingleAsync(cancellationToken));
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

    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-EXTERNAL-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.UseTransaction", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.TransactionUsed", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection", "Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task UseTransaction_EnlistsContextInExternalTransactionSynchronously()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var id = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        var interceptor = new RecordingExternalTransactionInterceptor();

        await using (var initialize = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(initialize, cancellationToken);
        }

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
            var connection = context.Database.GetDbConnection();
            await using var externalTransaction = await connection.BeginTransactionAsync(cancellationToken);
            using var efTransaction = context.Database.UseTransaction(externalTransaction);

            Assert.NotNull(efTransaction);
            Assert.Same(externalTransaction, efTransaction.GetDbTransaction());
            Assert.Same(efTransaction, context.Database.CurrentTransaction);
            context.People.Add(new Person { Id = id, Name = "External sync rollback", Age = 31 });
            Assert.Equal(1, context.SaveChanges());
            efTransaction.Rollback();
        }

        Assert.Equal(1, interceptor.TransactionUsedCalls);
        Assert.Equal(0, interceptor.TransactionUsedAsyncCalls);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(person => person.Id == id, cancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-EXTERNAL-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.UseTransactionAsync", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.TransactionUsedAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetDbConnection", "Microsoft.EntityFrameworkCore.Storage.DbContextTransactionExtensions.GetDbTransaction", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task UseTransactionAsync_EnlistsContextInExternalTransactionAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var id = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        var interceptor = new RecordingExternalTransactionInterceptor();

        await using (var initialize = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(initialize, cancellationToken);
        }

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
            var connection = context.Database.GetDbConnection();
            await using var externalTransaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var efTransaction = await context.Database.UseTransactionAsync(externalTransaction, cancellationToken);

            Assert.NotNull(efTransaction);
            Assert.Same(externalTransaction, efTransaction.GetDbTransaction());
            Assert.Same(efTransaction, context.Database.CurrentTransaction);
            context.People.Add(new Person { Id = id, Name = "External async rollback", Age = 32 });
            Assert.Equal(1, await context.SaveChangesAsync(cancellationToken));
            await efTransaction.RollbackAsync(cancellationToken);
        }

        Assert.Equal(0, interceptor.TransactionUsedCalls);
        Assert.Equal(1, interceptor.TransactionUsedAsyncCalls);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(person => person.Id == id, cancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-FACADE-COMMIT-ROLLBACK-SYNC", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CommitTransaction", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.RollbackTransaction", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task DatabaseFacadeCommitAndRollbackTransaction_ApplySynchronously()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var committedId = Guid.NewGuid();
        var rolledBackId = Guid.NewGuid();

        await using (var commit = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(commit, TestContext.Current.CancellationToken);
            using var transaction = commit.Database.BeginTransaction();
            commit.People.Add(new Person { Id = committedId, Name = "Facade committed", Age = 63 });
            Assert.Equal(1, commit.SaveChanges());
            commit.Database.CommitTransaction();
        }

        await using (var rollback = ProviderTestRuntime.CreateContext())
        {
            using var transaction = rollback.Database.BeginTransaction();
            rollback.People.Add(new Person { Id = rolledBackId, Name = "Facade rolled back", Age = 64 });
            Assert.Equal(1, rollback.SaveChanges());
            rollback.Database.RollbackTransaction();
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AnyAsync(person => person.Id == committedId, TestContext.Current.CancellationToken));
        Assert.False(await verify.People.AnyAsync(person => person.Id == rolledBackId, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-FACADE-COMMIT-ROLLBACK-ASYNC", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.CommitTransactionAsync", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.RollbackTransactionAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task DatabaseFacadeCommitAndRollbackTransaction_ApplyAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var committedId = Guid.NewGuid();
        var rolledBackId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var commit = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(commit, cancellationToken);
            await using var transaction = await commit.Database.BeginTransactionAsync(cancellationToken);
            commit.People.Add(new Person { Id = committedId, Name = "Facade committed async", Age = 65 });
            Assert.Equal(1, await commit.SaveChangesAsync(cancellationToken));
            await commit.Database.CommitTransactionAsync(cancellationToken);
        }

        await using (var rollback = ProviderTestRuntime.CreateContext())
        {
            await using var transaction = await rollback.Database.BeginTransactionAsync(cancellationToken);
            rollback.People.Add(new Person { Id = rolledBackId, Name = "Facade rolled back async", Age = 66 });
            Assert.Equal(1, await rollback.SaveChangesAsync(cancellationToken));
            await rollback.Database.RollbackTransactionAsync(cancellationToken);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AnyAsync(person => person.Id == committedId, cancellationToken));
        Assert.False(await verify.People.AnyAsync(person => person.Id == rolledBackId, cancellationToken));
    }

    private sealed class RecordingExternalTransactionInterceptor : DbTransactionInterceptor
    {
        public int TransactionUsedCalls { get; private set; }

        public int TransactionUsedAsyncCalls { get; private set; }

        public override DbTransaction TransactionUsed(DbConnection connection, TransactionEventData eventData, DbTransaction transaction)
        {
            TransactionUsedCalls++;
            return transaction;
        }

        public override ValueTask<DbTransaction> TransactionUsedAsync(
            DbConnection connection,
            TransactionEventData eventData,
            DbTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            TransactionUsedAsyncCalls++;
            return ValueTask.FromResult(transaction);
        }
    }
}
