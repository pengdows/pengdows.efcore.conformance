using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class PersistenceTests
{
    [Fact]
    [BehavioralCoverage("EF10-PERSIST-INSERT-ASYNC", "Microsoft.EntityFrameworkCore.DbSet`1.Add", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync", "System.Linq.Queryable.Where", "System.Linq.Queryable.Select")]
    [NativeTypeCoverage("VARCHAR(160)", typeof(string), "EF10-PERSIST-INSERT-ASYNC")]
    [NativeTypeCoverage("INTEGER", typeof(int), "EF10-PERSIST-INSERT-ASYNC")]
    [NativeTypeCoverage("TEXT", typeof(Guid), "EF10-PERSIST-INSERT-ASYNC")]
    public async Task SaveChanges_PersistsEntityForFreshContext()
    {
        var id = Guid.NewGuid();
        const string name = "Ada Lovelace";

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.People.Add(new Person { Id = id, Name = name, Age = 36 });
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People
            .Where(person => person.Id == id)
            .Select(person => new { person.Name, person.Age })
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(name, actual.Name);
        Assert.Equal(36, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-INSERT-SYNC", "Microsoft.EntityFrameworkCore.DbSet`1.Add", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single", "System.Linq.Queryable.Where", "System.Linq.Queryable.Select")]
    [NativeTypeCoverage("VARCHAR(160)", typeof(string), "EF10-PERSIST-INSERT-SYNC")]
    [NativeTypeCoverage("INTEGER", typeof(int), "EF10-PERSIST-INSERT-SYNC")]
    [NativeTypeCoverage("TEXT", typeof(Guid), "EF10-PERSIST-INSERT-SYNC")]
    public async Task SaveChanges_PersistsEntityForFreshContextSynchronously()
    {
        var id = Guid.NewGuid();
        const string name = "Grace Hopper";

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.People.Add(new Person { Id = id, Name = name, Age = 85 });
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.People
            .Where(person => person.Id == id)
            .Select(person => new { person.Name, person.Age })
            .Single();

        Assert.Equal(name, actual.Name);
        Assert.Equal(85, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-UPDATE-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking")]
    public async Task SaveChangesAsync_PersistsUpdatedValuesForFreshContext()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Before update", 24);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var person = await update.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
            person.Name = "After update";
            person.Age = 25;
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking().SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal("After update", actual.Name);
        Assert.Equal(25, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-UPDATE-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking")]
    public async Task SaveChanges_PersistsUpdatedValuesForFreshContextSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Before sync update", 44);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var person = update.People.Single(item => item.Id == id);
            person.Name = "After sync update";
            person.Age = 45;
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.People.AsNoTracking().Single(item => item.Id == id);
        Assert.Equal("After sync update", actual.Name);
        Assert.Equal(45, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-DELETE-ASYNC", "Microsoft.EntityFrameworkCore.DbSet`1.Remove", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync")]
    public async Task SaveChangesAsync_RemovesEntityForFreshContext()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Delete async", 31);

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var person = await delete.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
            delete.People.Remove(person);
            Assert.Equal(1, await delete.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(item => item.Id == id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-DELETE-SYNC", "Microsoft.EntityFrameworkCore.DbSet`1.Remove", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Any")]
    public async Task SaveChanges_RemovesEntityForFreshContextSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Delete sync", 32);

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var person = delete.People.Single(item => item.Id == id);
            delete.People.Remove(person);
            Assert.Equal(1, delete.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(verify.People.Any(item => item.Id == id));
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-ACCEPT-CHANGES-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AcceptAllChanges")]
    public async Task SaveChangesAsync_CanDeferAcceptingTrackedChanges()
    {
        var person = new Person { Id = Guid.NewGuid(), Name = "Deferred acceptance", Age = 27 };
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(person);

        Assert.Equal(1, await context.SaveChangesAsync(false, TestContext.Current.CancellationToken));
        Assert.Equal(EntityState.Added, context.Entry(person).State);
        context.ChangeTracker.AcceptAllChanges();
        Assert.Equal(EntityState.Unchanged, context.Entry(person).State);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-ACCEPT-CHANGES-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AcceptAllChanges")]
    public async Task SaveChanges_CanDeferAcceptingTrackedChangesSynchronously()
    {
        var person = new Person { Id = Guid.NewGuid(), Name = "Deferred sync acceptance", Age = 28 };
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(person);

        Assert.Equal(1, context.SaveChanges(false));
        Assert.Equal(EntityState.Added, context.Entry(person).State);
        context.ChangeTracker.AcceptAllChanges();
        Assert.Equal(EntityState.Unchanged, context.Entry(person).State);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-INSERT-FAILURE-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.DbUpdateException", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State")]
    public async Task SaveChangesAsync_FailedInsertLeavesTrackedEntityUnaccepted()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Existing row", 41);

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            write.People.Add(new Person { Id = id, Name = "Duplicate row", Age = 42 });

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                write.SaveChangesAsync(TestContext.Current.CancellationToken));

            Assert.Equal(EntityState.Added, write.ChangeTracker.Entries<Person>().Single().State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = await verify.People.AsNoTracking().SingleAsync(
            person => person.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal("Existing row", persisted.Name);
        Assert.Equal(41, persisted.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-INSERT-FAILURE-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.DbUpdateException", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State")]
    public async Task SaveChanges_FailedInsertLeavesTrackedEntityUnacceptedSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Existing sync row", 51);

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            write.People.Add(new Person { Id = id, Name = "Duplicate sync row", Age = 52 });

            Assert.Throws<DbUpdateException>(() => write.SaveChanges());

            Assert.Equal(EntityState.Added, write.ChangeTracker.Entries<Person>().Single().State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = await verify.People.AsNoTracking().SingleAsync(
            person => person.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal("Existing sync row", persisted.Name);
        Assert.Equal(51, persisted.Age);
    }

    private static async Task SeedAsync(Guid id, string name, int age)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = name, Age = age });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
