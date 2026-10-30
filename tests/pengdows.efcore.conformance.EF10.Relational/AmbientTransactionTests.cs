using System.Transactions;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("ambient-transactions")]
public sealed class AmbientTransactionTests
{
    [Fact]
    [CapabilityRequirement("transactions")]
    [BehavioralCoverage("EF10-AMBIENT-TRANSACTION-SYNC", "System.Transactions.TransactionsDatabaseFacadeExtensions.EnlistTransaction", "System.Transactions.TransactionsDatabaseFacadeExtensions.GetEnlistedTransaction", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task EnlistTransaction_PersistsSynchronousChangesWhenAmbientScopeCompletes()
    {
        ProviderTestRuntime.RequireCapability("ambient-transactions");
        ProviderTestRuntime.RequireCapability("transactions");
        var person = NewPerson("Ambient transaction sync");
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var setup = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(setup, cancellationToken);
        }

        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await using var context = ProviderTestRuntime.CreateContext();
            await context.Database.OpenConnectionAsync(cancellationToken);
            var ambient = Transaction.Current;
            Assert.NotNull(ambient);
            context.Database.EnlistTransaction(ambient);
            Assert.Same(ambient, context.Database.GetEnlistedTransaction());

            context.People.Add(person);
            Assert.Equal(1, context.SaveChanges());
            scope.Complete();
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(person.Name, await verify.People.AsNoTracking()
            .Where(item => item.Id == person.Id)
            .Select(item => item.Name)
            .SingleAsync(cancellationToken));
    }

    [Fact]
    [CapabilityRequirement("transactions")]
    [BehavioralCoverage("EF10-AMBIENT-TRANSACTION-ASYNC", "System.Transactions.TransactionsDatabaseFacadeExtensions.EnlistTransaction", "System.Transactions.TransactionsDatabaseFacadeExtensions.GetEnlistedTransaction", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task EnlistTransaction_PersistsAsynchronousChangesWhenAmbientScopeCompletes()
    {
        ProviderTestRuntime.RequireCapability("ambient-transactions");
        ProviderTestRuntime.RequireCapability("transactions");
        var person = NewPerson("Ambient transaction async");
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var setup = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(setup, cancellationToken);
        }

        using (var scope = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await using var context = ProviderTestRuntime.CreateContext();
            await context.Database.OpenConnectionAsync(cancellationToken);
            var ambient = Transaction.Current;
            Assert.NotNull(ambient);
            context.Database.EnlistTransaction(ambient);
            Assert.Same(ambient, context.Database.GetEnlistedTransaction());

            context.People.Add(person);
            Assert.Equal(1, await context.SaveChangesAsync(cancellationToken));
            scope.Complete();
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(person.Name, await verify.People.AsNoTracking()
            .Where(item => item.Id == person.Id)
            .Select(item => item.Name)
            .SingleAsync(cancellationToken));
    }

    private static Person NewPerson(string prefix) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{prefix} {Guid.NewGuid():N}",
        Age = 37
    };
}
