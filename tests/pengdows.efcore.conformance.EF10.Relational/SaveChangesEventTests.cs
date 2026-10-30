using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class SaveChangesEventTests
{
    [Fact]
    [BehavioralCoverage("EF10-SAVE-EVENTS-SUCCESS-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SavingChanges", "Microsoft.EntityFrameworkCore.DbContext.SavedChanges", "Microsoft.EntityFrameworkCore.SaveChangesEventArgs.AcceptAllChangesOnSuccess", "Microsoft.EntityFrameworkCore.SavedChangesEventArgs.EntitiesSavedCount", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task SaveChanges_RaisesEventsInOrderSynchronously()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        var events = new List<string>();
        var savingAcceptsChanges = false;
        var savedAcceptsChanges = false;
        context.SavingChanges += (_, args) =>
        {
            events.Add("saving");
            savingAcceptsChanges = args.AcceptAllChangesOnSuccess;
        };
        context.SavedChanges += (_, args) =>
        {
            events.Add($"saved:{args.EntitiesSavedCount}");
            savedAcceptsChanges = args.AcceptAllChangesOnSuccess;
        };
        context.People.Add(NewPerson("Sync event"));

        Assert.Equal(1, context.SaveChanges());
        Assert.Equal(new[] { "saving", "saved:1" }, events);
        Assert.True(savingAcceptsChanges);
        Assert.True(savedAcceptsChanges);
    }

    [Fact]
    [BehavioralCoverage("EF10-SAVE-EVENTS-SUCCESS-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SavingChanges", "Microsoft.EntityFrameworkCore.DbContext.SavedChanges", "Microsoft.EntityFrameworkCore.SaveChangesEventArgs.AcceptAllChangesOnSuccess", "Microsoft.EntityFrameworkCore.SavedChangesEventArgs.EntitiesSavedCount", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task SaveChangesAsync_RaisesEventsInOrder()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        var events = new List<string>();
        var savingAcceptsChanges = false;
        var savedAcceptsChanges = false;
        context.SavingChanges += (_, args) =>
        {
            events.Add("saving");
            savingAcceptsChanges = args.AcceptAllChangesOnSuccess;
        };
        context.SavedChanges += (_, args) =>
        {
            events.Add($"saved:{args.EntitiesSavedCount}");
            savedAcceptsChanges = args.AcceptAllChangesOnSuccess;
        };
        context.People.Add(NewPerson("Async event"));

        Assert.Equal(1, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "saving", "saved:1" }, events);
        Assert.True(savingAcceptsChanges);
        Assert.True(savedAcceptsChanges);
    }

    [Fact]
    [BehavioralCoverage("EF10-SAVE-EVENTS-FAILURE-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesFailed", "Microsoft.EntityFrameworkCore.SaveChangesFailedEventArgs.Exception", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.DbUpdateException")]
    public async Task SaveChanges_RaisesFailureEventSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var context = ProviderTestRuntime.CreateContext();
        Microsoft.EntityFrameworkCore.SaveChangesFailedEventArgs? observed = null;
        context.SaveChangesFailed += (_, args) => observed = args;
        context.People.Add(new Person { Id = id, Name = "Duplicate sync event", Age = 61 });

        Assert.Throws<DbUpdateException>(() => context.SaveChanges());
        Assert.NotNull(observed);
        Assert.IsType<DbUpdateException>(observed.Exception);
        Assert.True(observed.AcceptAllChangesOnSuccess);
    }

    [Fact]
    [BehavioralCoverage("EF10-SAVE-EVENTS-FAILURE-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesFailed", "Microsoft.EntityFrameworkCore.SaveChangesFailedEventArgs.Exception", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.DbUpdateException")]
    public async Task SaveChangesAsync_RaisesFailureEvent()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var context = ProviderTestRuntime.CreateContext();
        Microsoft.EntityFrameworkCore.SaveChangesFailedEventArgs? observed = null;
        context.SaveChangesFailed += (_, args) => observed = args;
        context.People.Add(new Person { Id = id, Name = "Duplicate async event", Age = 62 });

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
        Assert.NotNull(observed);
        Assert.IsType<DbUpdateException>(observed.Exception);
        Assert.True(observed.AcceptAllChangesOnSuccess);
    }

    private static Person NewPerson(string prefix) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{prefix} {Guid.NewGuid():N}",
        Age = 60
    };

    private static async Task SeedAsync(Guid id)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = "Already persisted", Age = 59 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
