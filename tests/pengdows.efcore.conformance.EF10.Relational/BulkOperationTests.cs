using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class BulkOperationTests
{
    [Fact]
    [BehavioralCoverage("EF10-BULK-UPDATE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync")]
    public async Task ExecuteUpdateAsync_ChangesMatchingRows()
    {
        var id = await SeedAsync();

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var affected = await update.People.Where(person => person.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(person => person.Age, person => person.Age + 1),
                    TestContext.Current.CancellationToken);
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(41, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-BULK-UPDATE-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdate")]
    public async Task ExecuteUpdate_ChangesMatchingRowsSynchronously()
    {
        var id = await SeedAsync();

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var affected = update.People.Where(person => person.Id == id)
                .ExecuteUpdate(setters => setters.SetProperty(person => person.Age, person => person.Age + 1));
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.People.AsNoTracking().Single(person => person.Id == id);
        Assert.Equal(41, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-BULK-DELETE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDeleteAsync")]
    public async Task ExecuteDeleteAsync_RemovesMatchingRows()
    {
        var id = await SeedAsync();

        await using var delete = ProviderTestRuntime.CreateContext();
        Assert.Equal(1, await delete.People.Where(person => person.Id == id)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken));

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(person => person.Id == id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-BULK-DELETE-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDelete")]
    public async Task ExecuteDelete_RemovesMatchingRowsSynchronously()
    {
        var id = await SeedAsync();

        await using var delete = ProviderTestRuntime.CreateContext();
        Assert.Equal(1, delete.People.Where(person => person.Id == id).ExecuteDelete());

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(verify.People.Any(person => person.Id == id));
    }

    private static async Task<Guid> SeedAsync()
    {
        var id = Guid.NewGuid();
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = $"Bulk {id:N}", Age = 40 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return id;
    }
}
