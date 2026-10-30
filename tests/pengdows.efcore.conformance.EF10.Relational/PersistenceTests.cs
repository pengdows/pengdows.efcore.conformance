using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class PersistenceTests
{
    [Fact]
    [BehavioralCoverage("EF10-PERSIST-ADD-ASYNC", "Microsoft.EntityFrameworkCore.DbSet`1.AddAsync", "Microsoft.EntityFrameworkCore.EntityState", "Microsoft.EntityFrameworkCore.EntityState.Added", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task AddAsync_TracksAndPersistsEntity()
    {
        var id = Guid.NewGuid();
        const string expectedName = "Async DbSet Add";
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, cancellationToken);
            var entry = await write.People.AddAsync(new Person { Id = id, Name = expectedName, Age = 38 }, cancellationToken);
            Assert.Equal(EntityState.Added, entry.State);
            Assert.Equal(1, await write.SaveChangesAsync(cancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, cancellationToken);
        Assert.Equal(expectedName, actual.Name);
        Assert.Equal(38, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-ADD-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.AddAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task DbContextAddAsync_TracksAndPersistsEntity()
    {
        var id = Guid.NewGuid();
        const string expectedName = "Async DbContext Add";
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, cancellationToken);
            var entry = await write.AddAsync(new Person { Id = id, Name = expectedName, Age = 39 }, cancellationToken);
            Assert.Equal(EntityState.Added, entry.State);
            Assert.Equal(1, await write.SaveChangesAsync(cancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, cancellationToken);
        Assert.Equal(expectedName, actual.Name);
        Assert.Equal(39, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-ADD-SYNC", "Microsoft.EntityFrameworkCore.DbContext.Add", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task DbContextAdd_TracksAndPersistsEntitySynchronously()
    {
        var person = new Person { Id = Guid.NewGuid(), Name = "Synchronous DbContext Add", Age = 40 };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            var entry = write.Add(person);
            Assert.Equal(EntityState.Added, entry.State);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken);
        Assert.Equal(person.Name, actual.Name);
        Assert.Equal(person.Age, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-ATTACH", "Microsoft.EntityFrameworkCore.DbContext.Attach", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.EntityState.Unchanged", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task DbContextAttach_TracksDetachedEntityAndPersistsChanges()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Attached through DbContext", 46);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var detached = new Person { Id = id, Name = "Attached through DbContext", Age = 46 };
            update.Attach(detached);
            Assert.Equal(EntityState.Unchanged, update.Entry(detached).State);
            detached.Age = 47;
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(47, await verify.People.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.Age)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-UPDATE", "Microsoft.EntityFrameworkCore.DbContext.Update", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.EntityState", "Microsoft.EntityFrameworkCore.EntityState.Modified", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task DbContextUpdate_TracksDetachedEntityAndPersistsChanges()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Before DbContext.Update", 48);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var detached = new Person { Id = id, Name = "After DbContext.Update", Age = 49 };
            update.Update(detached);
            Assert.Equal(EntityState.Modified, update.Entry(detached).State);
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal("After DbContext.Update", actual.Name);
        Assert.Equal(49, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-REMOVE", "Microsoft.EntityFrameworkCore.DbContext.Remove", "Microsoft.EntityFrameworkCore.EntityState", "Microsoft.EntityFrameworkCore.EntityState.Deleted", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync")]
    public async Task DbContextRemove_DeletesEntity()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Remove through DbContext", 50);

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var person = await delete.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
            var entry = delete.Remove(person);
            Assert.Equal(EntityState.Deleted, entry.State);
            Assert.Equal(1, delete.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(item => item.Id == id, TestContext.Current.CancellationToken));
    }

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
    [BehavioralCoverage("EF10-PERSIST-SAVE-NO-CHANGES-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task SaveChangesAsync_ReturnsZeroWhenThereAreNoChanges()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(0, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-SAVE-NO-CHANGES-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task SaveChanges_ReturnsZeroWhenThereAreNoChangesSynchronously()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(0, context.SaveChanges());
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
    [BehavioralCoverage("EF10-PERSIST-CANCELLATION", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task SaveChangesAsync_ObservesCancellationAndLeavesChangesPending()
    {
        var person = new Person { Id = Guid.NewGuid(), Name = "Canceled save", Age = 29 };
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.People.Add(person);
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await write.SaveChangesAsync(cancellation.Token));

            Assert.Equal(EntityState.Added, write.Entry(person).State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
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
    [BehavioralCoverage("EF10-PERSIST-INSERT-FAILURE-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.DbUpdateException", "Microsoft.EntityFrameworkCore.DbUpdateException.Entries", "Microsoft.EntityFrameworkCore.DbContext.ChangeTracker", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State")]
    public async Task SaveChangesAsync_FailedInsertLeavesTrackedEntityUnaccepted()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Existing row", 41);

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            var duplicate = new Person { Id = id, Name = "Duplicate row", Age = 42 };
            write.People.Add(duplicate);

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
                write.SaveChangesAsync(TestContext.Current.CancellationToken));

            Assert.Equal(EntityState.Added, write.ChangeTracker.Entries<Person>().Single().State);
            Assert.Same(duplicate, Assert.Single(exception.Entries).Entity);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = await verify.People.AsNoTracking().SingleAsync(
            person => person.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal("Existing row", persisted.Name);
        Assert.Equal(41, persisted.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-INSERT-FAILURE-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.DbUpdateException", "Microsoft.EntityFrameworkCore.DbUpdateException.Entries", "Microsoft.EntityFrameworkCore.DbContext.ChangeTracker", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State")]
    public async Task SaveChanges_FailedInsertLeavesTrackedEntityUnacceptedSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Existing sync row", 51);

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            var duplicate = new Person { Id = id, Name = "Duplicate sync row", Age = 52 };
            write.People.Add(duplicate);

            var exception = Assert.Throws<DbUpdateException>(() => write.SaveChanges());

            Assert.Equal(EntityState.Added, write.ChangeTracker.Entries<Person>().Single().State);
            Assert.Same(duplicate, Assert.Single(exception.Entries).Entity);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = await verify.People.AsNoTracking().SingleAsync(
            person => person.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal("Existing sync row", persisted.Name);
        Assert.Equal(51, persisted.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-ADD-RANGE-SYNC", "Microsoft.EntityFrameworkCore.DbSet`1.AddRange", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Any")]
    public async Task AddRange_PersistsEveryEntitySynchronously()
    {
        var people = CreatePeople("AddRange");
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.People.AddRange(people);
            Assert.Equal(people.Length, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        foreach (var person in people)
        {
            Assert.True(verify.People.Any(item => item.Id == person.Id && item.Name == person.Name));
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-ADD-RANGE-ASYNC", "Microsoft.EntityFrameworkCore.DbSet`1.AddRangeAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync")]
    public async Task AddRangeAsync_PersistsEveryEntity()
    {
        var people = CreatePeople("AddRangeAsync");
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            await write.People.AddRangeAsync(people, TestContext.Current.CancellationToken);
            Assert.Equal(people.Length, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        foreach (var person in people)
        {
            Assert.True(await verify.People.AnyAsync(
                item => item.Id == person.Id && item.Name == person.Name,
                TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-ADD-RANGE-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.AddRangeAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync")]
    public async Task DbContextAddRangeAsync_PersistsEveryEntity()
    {
        var people = CreatePeople("DbContextAddRangeAsync");
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, cancellationToken);
            await write.AddRangeAsync(people, cancellationToken);
            Assert.Equal(people.Length, await write.SaveChangesAsync(cancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        foreach (var person in people)
        {
            Assert.True(await verify.People.AnyAsync(
                item => item.Id == person.Id && item.Name == person.Name,
                cancellationToken));
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-ADD-RANGE-SYNC", "Microsoft.EntityFrameworkCore.DbContext.AddRange", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Any")]
    public async Task DbContextAddRange_PersistsEveryEntitySynchronously()
    {
        var people = CreatePeople("DbContextAddRange");

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.AddRange(people);
            Assert.Equal(people.Length, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        foreach (var person in people)
        {
            Assert.True(verify.People.Any(item => item.Id == person.Id && item.Name == person.Name));
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-ATTACH-RANGE", "Microsoft.EntityFrameworkCore.DbContext.AttachRange", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task DbContextAttachRange_TracksAndPersistsSubsequentChanges()
    {
        var existing = CreatePeople("DbContextAttachRange");
        foreach (var person in existing)
        {
            await SeedAsync(person.Id, person.Name, person.Age);
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var detached = existing.Select(person => new Person
            {
                Id = person.Id,
                Name = person.Name,
                Age = person.Age
            }).ToArray();

            update.AttachRange(detached);
            Assert.All(detached, person => Assert.Equal(EntityState.Unchanged, update.Entry(person).State));
            detached[0].Age++;
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(existing[0].Age + 1, await verify.People.AsNoTracking()
            .Where(person => person.Id == existing[0].Id)
            .Select(person => person.Age)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-UPDATE-RANGE", "Microsoft.EntityFrameworkCore.DbContext.UpdateRange", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task DbContextUpdateRange_PersistsDetachedEntityUpdates()
    {
        var existing = CreatePeople("DbContextUpdateRange");
        await SeedAsync(existing[0].Id, existing[0].Name, existing[0].Age);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var detached = new Person
            {
                Id = existing[0].Id,
                Name = "Updated through DbContext.UpdateRange",
                Age = existing[0].Age + 1
            };
            update.UpdateRange(detached);
            Assert.Equal(EntityState.Modified, update.Entry(detached).State);
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == existing[0].Id, TestContext.Current.CancellationToken);
        Assert.Equal("Updated through DbContext.UpdateRange", actual.Name);
        Assert.Equal(existing[0].Age + 1, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-REMOVE-RANGE", "Microsoft.EntityFrameworkCore.DbContext.RemoveRange", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync")]
    public async Task DbContextRemoveRange_RemovesEntities()
    {
        var people = CreatePeople("DbContextRemoveRange");
        foreach (var person in people)
        {
            await SeedAsync(person.Id, person.Name, person.Age);
        }

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var loaded = await delete.People
                .Where(person => people.Select(item => item.Id).Contains(person.Id))
                .ToListAsync(TestContext.Current.CancellationToken);
            delete.RemoveRange(loaded);
            Assert.Equal(people.Length, await delete.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        foreach (var person in people)
        {
            Assert.False(await verify.People.AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-UPDATE-RANGE", "Microsoft.EntityFrameworkCore.DbSet`1.UpdateRange", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task UpdateRange_PersistsAllDetachedEntityUpdates()
    {
        var existing = CreatePeople("UpdateRange");
        await SeedAsync(existing[0].Id, existing[0].Name, existing[0].Age);
        await SeedAsync(existing[1].Id, existing[1].Name, existing[1].Age);
        var updates = existing.Select(person => new Person
        {
            Id = person.Id,
            Name = $"Updated {person.Name}",
            Age = person.Age + 10
        }).ToArray();

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            write.People.UpdateRange(updates);
            Assert.Equal(2, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        foreach (var update in updates)
        {
            var actual = await verify.People.AsNoTracking().SingleAsync(
                item => item.Id == update.Id,
                TestContext.Current.CancellationToken);
            Assert.Equal(update.Name, actual.Name);
            Assert.Equal(update.Age, actual.Age);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-REMOVE-RANGE", "Microsoft.EntityFrameworkCore.DbSet`1.RemoveRange", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync")]
    public async Task RemoveRange_RemovesAllEntities()
    {
        var people = CreatePeople("RemoveRange");
        await SeedAsync(people[0].Id, people[0].Name, people[0].Age);
        await SeedAsync(people[1].Id, people[1].Name, people[1].Age);

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var ids = people.Select(person => person.Id).ToArray();
            var loaded = delete.People.Where(item => ids.Contains(item.Id)).ToArray();
            delete.People.RemoveRange(loaded);
            Assert.Equal(2, delete.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        foreach (var person in people)
        {
            Assert.False(await verify.People.AnyAsync(
                item => item.Id == person.Id,
                TestContext.Current.CancellationToken));
        }
    }

    private static Person[] CreatePeople(string prefix) =>
    [
        new Person { Id = Guid.NewGuid(), Name = $"{prefix} first {Guid.NewGuid():N}", Age = 21 },
        new Person { Id = Guid.NewGuid(), Name = $"{prefix} second {Guid.NewGuid():N}", Age = 22 }
    ];

    private static async Task SeedAsync(Guid id, string name, int age)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = name, Age = age });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
