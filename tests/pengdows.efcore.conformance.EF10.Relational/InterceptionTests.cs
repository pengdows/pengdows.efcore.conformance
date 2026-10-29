using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class InterceptionTests
{
    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-SAVE-SYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor.SavingChanges", "Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor.SavedChanges", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task SaveChanges_InvokesSynchronousInterceptorCallbacks()
    {
        var interceptor = new RecordingSaveChangesInterceptor();
        var person = CreatePerson("Sync interceptor");

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
            context.People.Add(person);

            Assert.Equal(1, context.SaveChanges());
        }

        Assert.Equal(1, interceptor.SavingChangesCalls);
        Assert.Equal(1, interceptor.SavedChangesCalls);
        Assert.Equal(0, interceptor.SavingChangesAsyncCalls);
        Assert.Equal(0, interceptor.SavedChangesAsyncCalls);
        Assert.Equal(1, interceptor.LastSavedChangesResult);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-SAVE-ASYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor.SavingChangesAsync", "Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor.SavedChangesAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task SaveChangesAsync_InvokesAsynchronousInterceptorCallbacks()
    {
        var interceptor = new RecordingSaveChangesInterceptor();
        var person = CreatePerson("Async interceptor");

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
            context.People.Add(person);

            Assert.Equal(1, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        Assert.Equal(0, interceptor.SavingChangesCalls);
        Assert.Equal(0, interceptor.SavedChangesCalls);
        Assert.Equal(1, interceptor.SavingChangesAsyncCalls);
        Assert.Equal(1, interceptor.SavedChangesAsyncCalls);
        Assert.Equal(1, interceptor.LastSavedChangesResult);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
    }

    private static Person CreatePerson(string prefix) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{prefix} {Guid.NewGuid():N}",
        Age = 39
    };

    private sealed class RecordingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public int SavingChangesCalls { get; private set; }

        public int SavedChangesCalls { get; private set; }

        public int SavingChangesAsyncCalls { get; private set; }

        public int SavedChangesAsyncCalls { get; private set; }

        public int LastSavedChangesResult { get; private set; }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            SavingChangesCalls++;
            return result;
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            SavedChangesCalls++;
            LastSavedChangesResult = result;
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            SavingChangesAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            SavedChangesAsyncCalls++;
            LastSavedChangesResult = result;
            return ValueTask.FromResult(result);
        }
    }
}
