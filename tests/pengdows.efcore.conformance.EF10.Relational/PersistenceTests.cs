using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-ADD-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.AddAsync", "System.Threading.Tasks.ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity>> AddAsync<TEntity>(TEntity entity, System.Threading.CancellationToken cancellationToken = null) where TEntity : class", "System.Threading.Tasks.ValueTask<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> AddAsync(System.Object entity, System.Threading.CancellationToken cancellationToken = null)", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task DbContextAddAsync_TracksAndPersistsEntity()
    {
        var id = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        const string expectedName = "Async DbContext Add";
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, cancellationToken);
            var entry = await write.AddAsync(new Person { Id = id, Name = expectedName, Age = 39 }, cancellationToken);
            var objectEntry = await write.AddAsync((object)new Person { Id = objectId, Name = expectedName + " object", Age = 40 }, cancellationToken);
            Assert.Equal(EntityState.Added, entry.State);
            Assert.Equal(EntityState.Added, objectEntry.State);
            Assert.Equal(2, await write.SaveChangesAsync(cancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, cancellationToken);
        Assert.Equal(expectedName, actual.Name);
        Assert.Equal(39, actual.Age);
        var objectActual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == objectId, cancellationToken);
        Assert.Equal(expectedName + " object", objectActual.Name);
        Assert.Equal(40, objectActual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-ADD-SYNC", "Microsoft.EntityFrameworkCore.DbContext.Add", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> Add<TEntity>(TEntity entity) where TEntity : class", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Add(System.Object entity)", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task DbContextAdd_TracksAndPersistsEntitySynchronously()
    {
        var person = new Person { Id = Guid.NewGuid(), Name = "Synchronous DbContext Add", Age = 40 };
        var objectPerson = new Person { Id = Guid.NewGuid(), Name = "Synchronous DbContext Add object", Age = 41 };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            var entry = write.Add(person);
            var objectEntry = write.Add((object)objectPerson);
            Assert.Equal(EntityState.Added, entry.State);
            Assert.Equal(EntityState.Added, objectEntry.State);
            Assert.Equal(2, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken);
        Assert.Equal(person.Name, actual.Name);
        Assert.Equal(person.Age, actual.Age);
        var objectActual = await verify.People.AsNoTracking()
            .SingleAsync(item => item.Id == objectPerson.Id, TestContext.Current.CancellationToken);
        Assert.Equal(objectPerson.Name, objectActual.Name);
        Assert.Equal(objectPerson.Age, objectActual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-ATTACH", "Microsoft.EntityFrameworkCore.DbContext.Attach", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> Attach<TEntity>(TEntity entity) where TEntity : class", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Attach(System.Object entity)", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry(System.Object entity)", "Microsoft.EntityFrameworkCore.EntityState.Unchanged", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task DbContextAttach_TracksDetachedEntityAndPersistsChanges()
    {
        var id = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        await SeedAsync(id, "Attached through DbContext", 46);
        await SeedAsync(objectId, "Attached through DbContext object", 48);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var detached = new Person { Id = id, Name = "Attached through DbContext", Age = 46 };
            var objectDetached = new Person { Id = objectId, Name = "Attached through DbContext object", Age = 48 };
            update.Attach(detached);
            update.Attach((object)objectDetached);
            Assert.Equal(EntityState.Unchanged, update.Entry(detached).State);
            Assert.Equal(EntityState.Unchanged, update.Entry((object)objectDetached).State);
            Assert.Same(detached, update.Entry((object)detached).Entity);
            detached.Age = 47;
            objectDetached.Age = 49;
            Assert.Equal(2, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(47, await verify.People.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.Age)
            .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(49, await verify.People.AsNoTracking()
            .Where(item => item.Id == objectId)
            .Select(item => item.Age)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-UPDATE", "Microsoft.EntityFrameworkCore.DbContext.Update", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> Update<TEntity>(TEntity entity) where TEntity : class", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Update(System.Object entity)", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.EntityState", "Microsoft.EntityFrameworkCore.EntityState.Modified", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task DbContextUpdate_TracksDetachedEntityAndPersistsChanges()
    {
        var id = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        await SeedAsync(id, "Before DbContext.Update", 48);
        await SeedAsync(objectId, "Before DbContext.Update object", 50);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var detached = new Person { Id = id, Name = "After DbContext.Update", Age = 49 };
            var objectDetached = new Person { Id = objectId, Name = "After DbContext.Update object", Age = 51 };
            update.Update(detached);
            update.Update((object)objectDetached);
            Assert.Equal(EntityState.Modified, update.Entry(detached).State);
            Assert.Equal(EntityState.Modified, update.Entry((object)objectDetached).State);
            Assert.Equal(2, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal("After DbContext.Update", actual.Name);
        Assert.Equal(49, actual.Age);
        var objectActual = await verify.People.AsNoTracking()
            .SingleAsync(item => item.Id == objectId, TestContext.Current.CancellationToken);
        Assert.Equal("After DbContext.Update object", objectActual.Name);
        Assert.Equal(51, objectActual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-REMOVE", "Microsoft.EntityFrameworkCore.DbContext.Remove", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TEntity> Remove<TEntity>(TEntity entity) where TEntity : class", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Remove(System.Object entity)", "Microsoft.EntityFrameworkCore.EntityState", "Microsoft.EntityFrameworkCore.EntityState.Deleted", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync")]
    public async Task DbContextRemove_DeletesEntity()
    {
        var id = Guid.NewGuid();
        var objectId = Guid.NewGuid();
        await SeedAsync(id, "Remove through DbContext", 50);
        await SeedAsync(objectId, "Remove through DbContext object", 52);

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var person = await delete.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
            var objectPerson = await delete.People.SingleAsync(item => item.Id == objectId, TestContext.Current.CancellationToken);
            var entry = delete.Remove(person);
            var objectEntry = delete.Remove((object)objectPerson);
            Assert.Equal(EntityState.Deleted, entry.State);
            Assert.Equal(EntityState.Deleted, objectEntry.State);
            Assert.Equal(2, delete.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(item => item.Id == id, TestContext.Current.CancellationToken));
        Assert.False(await verify.People.AnyAsync(item => item.Id == objectId, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-INSERT-ASYNC", "Microsoft.EntityFrameworkCore.DbSet`1.Add", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync", "System.Linq.Queryable.Where", "System.Linq.Queryable.Select")]
    [NativeTypeCoverage("VARCHAR(160)", typeof(string), "EF10-PERSIST-INSERT-ASYNC")]
    [NativeTypeCoverage("INTEGER", typeof(int), "EF10-PERSIST-INSERT-ASYNC")]
    [NativeTypeCoverage("TEXT", typeof(Guid), "EF10-PERSIST-INSERT-ASYNC")]
    [NativeTypeCoverage("CHARACTER VARYING", typeof(string), "EF10-PERSIST-INSERT-ASYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("UUID", typeof(Guid), "EF10-PERSIST-INSERT-ASYNC", "Npgsql EF Core PostgreSQL")]
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
    [NativeTypeCoverage("CHARACTER VARYING", typeof(string), "EF10-PERSIST-INSERT-SYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("UUID", typeof(Guid), "EF10-PERSIST-INSERT-SYNC", "Npgsql EF Core PostgreSQL")]
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
    [BehavioralCoverage("EF10-PERSIST-DELETE-SYNC", "Microsoft.EntityFrameworkCore.DbSet`1.Remove", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Int32 SaveChanges()", "System.Linq.Queryable.Any")]
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
    [BehavioralCoverage("EF10-PERSIST-CANCELLATION", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "System.Threading.Tasks.Task<System.Int32> SaveChangesAsync(System.Threading.CancellationToken cancellationToken = null)")]
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
    [BehavioralCoverage("EF10-PERSIST-INSERT-FAILURE-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.DbUpdateException", "Microsoft.EntityFrameworkCore.DbUpdateException.Entries", "Microsoft.EntityFrameworkCore.DbContext.ChangeTracker", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State", "Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor.CommandFailedAsync")]
    public async Task SaveChangesAsync_FailedInsertLeavesTrackedEntityUnaccepted()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Existing row", 41);
        var interceptor = new RecordingFailureCommandInterceptor();

        await using (var write = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            var duplicate = new Person { Id = id, Name = "Duplicate row", Age = 42 };
            write.People.Add(duplicate);

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
                write.SaveChangesAsync(TestContext.Current.CancellationToken));

            Assert.Equal(EntityState.Added, write.ChangeTracker.Entries<Person>().Single().State);
            Assert.Same(duplicate, Assert.Single(exception.Entries).Entity);
        }

        Assert.Equal(0, interceptor.CommandFailedCalls);
        Assert.Equal(1, interceptor.CommandFailedAsyncCalls);

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = await verify.People.AsNoTracking().SingleAsync(
            person => person.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal("Existing row", persisted.Name);
        Assert.Equal(41, persisted.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-INSERT-FAILURE-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.DbUpdateException", "Microsoft.EntityFrameworkCore.DbUpdateException.Entries", "Microsoft.EntityFrameworkCore.DbContext.ChangeTracker", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State", "Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor.CommandFailed")]
    public async Task SaveChanges_FailedInsertLeavesTrackedEntityUnacceptedSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Existing sync row", 51);
        var interceptor = new RecordingFailureCommandInterceptor();

        await using (var write = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            var duplicate = new Person { Id = id, Name = "Duplicate sync row", Age = 52 };
            write.People.Add(duplicate);

            var exception = Assert.Throws<DbUpdateException>(() => write.SaveChanges());

            Assert.Equal(EntityState.Added, write.ChangeTracker.Entries<Person>().Single().State);
            Assert.Same(duplicate, Assert.Single(exception.Entries).Entity);
        }

        Assert.Equal(1, interceptor.CommandFailedCalls);
        Assert.Equal(0, interceptor.CommandFailedAsyncCalls);

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = await verify.People.AsNoTracking().SingleAsync(
            person => person.Id == id,
            TestContext.Current.CancellationToken);
        Assert.Equal("Existing sync row", persisted.Name);
        Assert.Equal(51, persisted.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-BATCH-FAILURE-ATOMIC-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.DbUpdateException", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task SaveChangesAsync_FailedBatchDoesNotPersistOtherAddedEntities()
    {
        var duplicateId = Guid.NewGuid();
        var validId = Guid.NewGuid();
        await SeedAsync(duplicateId, "Existing batch row", 71);
        var duplicate = new Person { Id = duplicateId, Name = "Duplicate batch row", Age = 72 };
        var otherwiseValid = new Person { Id = validId, Name = "Otherwise valid batch row", Age = 73 };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            write.People.AddRange(duplicate, otherwiseValid);

            await Assert.ThrowsAsync<DbUpdateException>(() =>
                write.SaveChangesAsync(TestContext.Current.CancellationToken));

            Assert.Equal(2, write.ChangeTracker.Entries<Person>().Count());
            Assert.All(write.ChangeTracker.Entries<Person>(), entry => Assert.Equal(EntityState.Added, entry.State));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = await verify.People.AsNoTracking()
            .Where(person => person.Id == duplicateId || person.Id == validId)
            .ToListAsync(TestContext.Current.CancellationToken);
        var original = Assert.Single(persisted);
        Assert.Equal(duplicateId, original.Id);
        Assert.Equal("Existing batch row", original.Name);
        Assert.Equal(71, original.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-BATCH-FAILURE-ATOMIC-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.DbUpdateException", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker", "System.Linq.Queryable.ToList")]
    public async Task SaveChanges_FailedBatchDoesNotPersistOtherAddedEntitiesSynchronously()
    {
        var duplicateId = Guid.NewGuid();
        var validId = Guid.NewGuid();
        await SeedAsync(duplicateId, "Existing sync batch row", 81);
        var duplicate = new Person { Id = duplicateId, Name = "Duplicate sync batch row", Age = 82 };
        var otherwiseValid = new Person { Id = validId, Name = "Otherwise valid sync batch row", Age = 83 };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            write.People.AddRange(duplicate, otherwiseValid);

            Assert.Throws<DbUpdateException>(() => write.SaveChanges());

            Assert.Equal(2, write.ChangeTracker.Entries<Person>().Count());
            Assert.All(write.ChangeTracker.Entries<Person>(), entry => Assert.Equal(EntityState.Added, entry.State));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var persisted = verify.People.AsNoTracking()
            .Where(person => person.Id == duplicateId || person.Id == validId)
            .ToList();
        var original = Assert.Single(persisted);
        Assert.Equal(duplicateId, original.Id);
        Assert.Equal("Existing sync batch row", original.Name);
        Assert.Equal(81, original.Age);
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
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-ATTACH-RANGE", "Microsoft.EntityFrameworkCore.DbContext.AttachRange", "System.Void AttachRange(System.Object[] entities)", "System.Void AttachRange(System.Collections.Generic.IEnumerable<System.Object> entities)", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
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

            update.AttachRange((object[])detached.Take(1).ToArray());
            update.AttachRange((IEnumerable<object>)detached.Skip(1));
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
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-UPDATE-RANGE", "Microsoft.EntityFrameworkCore.DbContext.UpdateRange", "System.Void UpdateRange(System.Object[] entities)", "System.Void UpdateRange(System.Collections.Generic.IEnumerable<System.Object> entities)", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task DbContextUpdateRange_PersistsDetachedEntityUpdates()
    {
        var existing = CreatePeople("DbContextUpdateRange");
        await SeedAsync(existing[0].Id, existing[0].Name, existing[0].Age);
        await SeedAsync(existing[1].Id, existing[1].Name, existing[1].Age);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var detachedArray = new[] { new Person
            {
                Id = existing[0].Id,
                Name = "Updated through DbContext.UpdateRange array",
                Age = existing[0].Age + 1
            } };
            var detachedEnumerable = new[] { new Person
            {
                Id = existing[1].Id,
                Name = "Updated through DbContext.UpdateRange enumerable",
                Age = existing[1].Age + 1
            } };
            update.UpdateRange((object[])detachedArray);
            update.UpdateRange((IEnumerable<object>)detachedEnumerable);
            Assert.Equal(EntityState.Modified, update.Entry(detachedArray[0]).State);
            Assert.Equal(EntityState.Modified, update.Entry(detachedEnumerable[0]).State);
            Assert.Equal(2, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == existing[0].Id, TestContext.Current.CancellationToken);
        Assert.Equal("Updated through DbContext.UpdateRange array", actual.Name);
        Assert.Equal(existing[0].Age + 1, actual.Age);
        var enumerableActual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == existing[1].Id, TestContext.Current.CancellationToken);
        Assert.Equal("Updated through DbContext.UpdateRange enumerable", enumerableActual.Name);
        Assert.Equal(existing[1].Age + 1, enumerableActual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-PERSIST-CONTEXT-REMOVE-RANGE", "Microsoft.EntityFrameworkCore.DbContext.RemoveRange", "System.Void RemoveRange(System.Object[] entities)", "System.Void RemoveRange(System.Collections.Generic.IEnumerable<System.Object> entities)", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync")]
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
            delete.RemoveRange((object[])loaded.Take(1).ToArray());
            delete.RemoveRange((IEnumerable<object>)loaded.Skip(1));
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

    private sealed class RecordingFailureCommandInterceptor : DbCommandInterceptor
    {
        public int CommandFailedCalls { get; private set; }

        public int CommandFailedAsyncCalls { get; private set; }

        public override void CommandFailed(DbCommand command, CommandErrorEventData eventData) =>
            CommandFailedCalls++;

        public override Task CommandFailedAsync(
            DbCommand command,
            CommandErrorEventData eventData,
            CancellationToken cancellationToken = default)
        {
            CommandFailedAsyncCalls++;
            return Task.CompletedTask;
        }
    }
}
