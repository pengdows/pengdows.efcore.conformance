using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class TransactionTests
{
    [Fact]
    [BehavioralCoverage("EF10-TRANSACTION-ROLLBACK-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.BeginTransactionAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.RollbackAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task TransactionRollback_DoesNotPersistSavedChanges()
    {
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
}
