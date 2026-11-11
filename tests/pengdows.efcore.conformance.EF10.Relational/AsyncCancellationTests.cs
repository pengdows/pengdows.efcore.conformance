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
    [BehavioralCoverage("EF10-ASYNC-QUERY-CANCELLATION-DURING-ENUMERATION", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsAsyncEnumerable", "System.Collections.Generic.IAsyncEnumerator`1.MoveNextAsync")]
    public async Task AsyncEnumeration_ObservesCancellationAfterReturningAnItemAndContextRemainsUsable()
    {
        var batch = $"cancel during enumeration {Guid.NewGuid():N}";
        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.People.AddRange(Enumerable.Range(0, 128).Select(index => new Person
            {
                Id = Guid.NewGuid(),
                Name = $"{batch}-{index:D3}",
                Age = index
            }));
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = ProviderTestRuntime.CreateContext();
        using var cancellation = new CancellationTokenSource();
        await using (var enumerator = context.People.AsNoTracking()
            .Where(person => person.Name.StartsWith(batch))
            .OrderBy(person => person.Name)
            .AsAsyncEnumerable()
            .GetAsyncEnumerator(cancellation.Token))
        {
            Assert.True(await enumerator.MoveNextAsync());
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumerator.MoveNextAsync().AsTask());
        }

        Assert.Equal(128, await context.People.AsNoTracking()
            .CountAsync(person => person.Name.StartsWith(batch), TestContext.Current.CancellationToken));
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
