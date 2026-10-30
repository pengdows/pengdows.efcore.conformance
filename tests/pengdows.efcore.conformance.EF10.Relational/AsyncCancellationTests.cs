using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class AsyncCancellationTests
{
    [Fact]
    [BehavioralCoverage("EF10-ASYNC-QUERY-CANCELLATION", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task ToListAsync_ObservesCancellationBeforeReturningResults()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.People
            .Where(person => person.Age > 0)
            .ToListAsync(cancellation.Token));
    }

    [Fact]
    [BehavioralCoverage("EF10-ASYNC-SAVE-CANCELLATION", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync")]
    public async Task SaveChangesAsync_ObservesCancellationWithoutPersistingThePendingEntity()
    {
        var id = Guid.NewGuid();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.People.Add(new Person { Id = id, Name = $"cancelled save {id:N}", Age = 42 });
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                write.SaveChangesAsync(cancellation.Token));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.IgnoreQueryFilters().AnyAsync(
            person => person.Id == id,
            TestContext.Current.CancellationToken));
    }
}
