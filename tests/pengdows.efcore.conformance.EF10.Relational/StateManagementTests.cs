using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class StateManagementTests
{
    [Fact]
    [BehavioralCoverage("EF10-TRACKING-FIND", "Microsoft.EntityFrameworkCore.DbSet`1.Find", "Microsoft.EntityFrameworkCore.DbContext.Entry")]
    public async Task Find_ReturnsTrackedEntityAndReusesItSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var context = ProviderTestRuntime.CreateContext();
        var first = context.People.Find(id);
        var second = context.People.Find(id);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(EntityState.Unchanged, context.Entry(first).State);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-FIND-ASYNC", "Microsoft.EntityFrameworkCore.DbSet`1.FindAsync", "Microsoft.EntityFrameworkCore.DbContext.Entry")]
    public async Task FindAsync_ReturnsTrackedEntityAndReusesIt()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var context = ProviderTestRuntime.CreateContext();
        var first = await context.People.FindAsync([id], TestContext.Current.CancellationToken);
        var second = await context.People.FindAsync([id], TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.Same(first, second);
        Assert.Equal(EntityState.Unchanged, context.Entry(first).State);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-DBCONTEXT-FIND", "Microsoft.EntityFrameworkCore.DbContext.Find", "Microsoft.EntityFrameworkCore.DbContext.Entry")]
    public async Task DbContextFind_LoadsEntityByKeySynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var context = ProviderTestRuntime.CreateContext();
        var person = context.Find<Person>(id);

        Assert.NotNull(person);
        Assert.Equal(id, person.Id);
        Assert.Equal($"Tracked {id:N}", person.Name);
        Assert.Equal(EntityState.Unchanged, context.Entry(person).State);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-DBCONTEXT-FIND-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.FindAsync", "Microsoft.EntityFrameworkCore.DbContext.Entry")]
    public async Task DbContextFindAsync_LoadsEntityByKeyAsynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var context = ProviderTestRuntime.CreateContext();
        var person = await context.FindAsync<Person>([id], TestContext.Current.CancellationToken);

        Assert.NotNull(person);
        Assert.Equal(id, person.Id);
        Assert.Equal($"Tracked {id:N}", person.Name);
        Assert.Equal(EntityState.Unchanged, context.Entry(person).State);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-FIND-MISSING-SYNC", "Microsoft.EntityFrameworkCore.DbSet`1.Find")]
    public async Task Find_ReturnsNullForMissingKeySynchronously()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.Null(context.People.Find(Guid.NewGuid()));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-FIND-MISSING-ASYNC", "Microsoft.EntityFrameworkCore.DbSet`1.FindAsync")]
    public async Task FindAsync_ReturnsNullForMissingKey()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.Null(await context.People.FindAsync([Guid.NewGuid()], TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-NO-TRACKING", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking", "Microsoft.EntityFrameworkCore.DbContext.Entry")]
    public async Task AsNoTracking_MaterializesDetachedEntity()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var context = ProviderTestRuntime.CreateContext();
        var person = await context.People.AsNoTracking()
            .SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);

        Assert.Equal(EntityState.Detached, context.Entry(person).State);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-AS-TRACKING-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsTracking", "Microsoft.EntityFrameworkCore.DbContext.Entry")]
    public async Task AsTracking_OverridesNoTrackingContextDefaultAsynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var context = ProviderTestRuntime.CreateContext(options =>
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        var person = await context.People.AsTracking()
            .SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);

        Assert.Equal(EntityState.Unchanged, context.Entry(person).State);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-AS-TRACKING-OVERRIDE-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsTracking", "Microsoft.EntityFrameworkCore.DbContext.Entry")]
    public async Task AsTracking_UsesRequestedQueryTrackingBehaviorSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var context = ProviderTestRuntime.CreateContext();
        var person = context.People.AsTracking(QueryTrackingBehavior.NoTracking)
            .Single(item => item.Id == id);

        Assert.Equal(EntityState.Detached, context.Entry(person).State);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-CONTEXT-BEHAVIOR-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.QueryTrackingBehavior", "Microsoft.EntityFrameworkCore.DbContext.Entry")]
    public async Task ChangeTrackerQueryTrackingBehavior_ControlsAsyncMaterialization()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var context = ProviderTestRuntime.CreateContext();
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        var detached = await context.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(EntityState.Detached, context.Entry(detached).State);

        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var tracked = await context.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(EntityState.Unchanged, context.Entry(tracked).State);
        Assert.NotSame(detached, tracked);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-CONTEXT-BEHAVIOR-SYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.QueryTrackingBehavior", "Microsoft.EntityFrameworkCore.DbContext.Entry")]
    public async Task ChangeTrackerQueryTrackingBehavior_ControlsSynchronousMaterialization()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var context = ProviderTestRuntime.CreateContext();
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        var detached = context.People.Single(item => item.Id == id);
        Assert.Equal(EntityState.Detached, context.Entry(detached).State);

        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var tracked = context.People.Single(item => item.Id == id);
        Assert.Equal(EntityState.Unchanged, context.Entry(tracked).State);
        Assert.NotSame(detached, tracked);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-NO-TRACKING-IDENTITY-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync", "System.Linq.Queryable.Join")]
    public async Task AsNoTrackingWithIdentityResolution_ReusesRepeatedEntityInstancesAsynchronously()
    {
        var postId = await SeedPostWithTwoCommentsAsync();

        await using var context = ProviderTestRuntime.CreateContext();
        var posts = await context.Set<Post>()
            .Where(post => post.Id == postId)
            .Join(context.Set<Comment>(), post => post.Id, comment => comment.PostId, (post, _) => post)
            .OrderBy(post => post.Id)
            .AsNoTrackingWithIdentityResolution()
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, posts.Count);
        Assert.Same(posts[0], posts[1]);
        Assert.Empty(context.ChangeTracker.Entries<Post>());
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-NO-TRACKING-IDENTITY-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTrackingWithIdentityResolution", "System.Linq.Queryable.ToList", "System.Linq.Queryable.Join")]
    public async Task AsNoTrackingWithIdentityResolution_ReusesRepeatedEntityInstancesSynchronously()
    {
        var postId = await SeedPostWithTwoCommentsAsync();

        await using var context = ProviderTestRuntime.CreateContext();
        var posts = context.Set<Post>()
            .Where(post => post.Id == postId)
            .Join(context.Set<Comment>(), post => post.Id, comment => comment.PostId, (post, _) => post)
            .OrderBy(post => post.Id)
            .AsNoTrackingWithIdentityResolution()
            .ToList();

        Assert.Equal(2, posts.Count);
        Assert.Same(posts[0], posts[1]);
        Assert.Empty(context.ChangeTracker.Entries<Post>());
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-ATTACH", "Microsoft.EntityFrameworkCore.DbSet`1.Attach", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task Attach_TracksEntityAndPersistsSubsequentChanges()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var attached = new Person { Id = id, Name = $"Tracked {id:N}", Age = 50 };
            update.People.Attach(attached);
            Assert.Equal(EntityState.Unchanged, update.Entry(attached).State);

            attached.Age = 52;
            Assert.Equal(EntityState.Modified, update.Entry(attached).State);
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal($"Tracked {id:N}", actual.Name);
        Assert.Equal(52, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-UPDATE-DETACHED-ASYNC", "Microsoft.EntityFrameworkCore.DbSet`1.Update", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task Update_TracksDetachedEntityAndPersistsChangesAsynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var detached = new Person { Id = id, Name = $"Updated {id:N}", Age = 61 };
            update.People.Update(detached);
            Assert.Equal(EntityState.Modified, update.Entry(detached).State);
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal($"Updated {id:N}", actual.Name);
        Assert.Equal(61, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-UPDATE-DETACHED-SYNC", "Microsoft.EntityFrameworkCore.DbSet`1.Update", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task Update_TracksDetachedEntityAndPersistsChangesSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var detached = new Person { Id = id, Name = $"Updated sync {id:N}", Age = 62 };
            update.People.Update(detached);
            Assert.Equal(EntityState.Modified, update.Entry(detached).State);
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.People.AsNoTracking().Single(person => person.Id == id);
        Assert.Equal($"Updated sync {id:N}", actual.Name);
        Assert.Equal(62, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-DETACH", "Microsoft.EntityFrameworkCore.DbContext.Entry")]
    public async Task EntryDetach_RemovesEntityFromContextTracking()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var context = ProviderTestRuntime.CreateContext();
        var person = await context.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        context.Entry(person).State = EntityState.Detached;

        Assert.Equal(EntityState.Detached, context.Entry(person).State);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-DATABASE-VALUES-SYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.GetDatabaseValues", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues.GetValue")]
    public async Task GetDatabaseValues_ReadsCurrentDatabaseStateWithoutReplacingTrackedValues()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var trackedContext = ProviderTestRuntime.CreateContext();
        var person = await trackedContext.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        await UpdateNameAsync(id, "Updated by another context");

        var databaseValues = trackedContext.Entry(person).GetDatabaseValues();

        Assert.NotNull(databaseValues);
        Assert.Equal("Updated by another context", databaseValues.GetValue<string>(nameof(Person.Name)));
        Assert.Equal($"Tracked {id:N}", person.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-DATABASE-VALUES-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.GetDatabaseValuesAsync", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues.GetValue")]
    public async Task GetDatabaseValuesAsync_ReadsCurrentDatabaseStateWithoutReplacingTrackedValues()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var trackedContext = ProviderTestRuntime.CreateContext();
        var person = await trackedContext.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        await UpdateNameAsync(id, "Updated by another async context");

        var databaseValues = await trackedContext.Entry(person).GetDatabaseValuesAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(databaseValues);
        Assert.Equal("Updated by another async context", databaseValues.GetValue<string>(nameof(Person.Name)));
        Assert.Equal($"Tracked {id:N}", person.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-HAS-CHANGES", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.HasChanges", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task HasChanges_TracksPendingPersistenceWork()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);

        Assert.False(context.ChangeTracker.HasChanges());
        context.People.Add(new Person { Id = Guid.NewGuid(), Name = "Pending change", Age = 33 });
        Assert.True(context.ChangeTracker.HasChanges());
        Assert.Equal(1, context.SaveChanges());
        Assert.False(context.ChangeTracker.HasChanges());
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-DETECT-CHANGES-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AutoDetectChangesEnabled", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.DetectChanges", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task DetectChanges_ExplicitlyFindsAndPersistsChangesAsynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            update.ChangeTracker.AutoDetectChangesEnabled = false;
            var person = await update.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
            person.Name = "Explicit async detection";

            Assert.Equal(EntityState.Unchanged, update.Entry(person).State);
            update.ChangeTracker.DetectChanges();
            Assert.Equal(EntityState.Modified, update.Entry(person).State);
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("Explicit async detection", await verify.People.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-DETECT-CHANGES-SYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AutoDetectChangesEnabled", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.DetectChanges", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task DetectChanges_ExplicitlyFindsAndPersistsChangesSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            update.ChangeTracker.AutoDetectChangesEnabled = false;
            var person = update.People.Single(item => item.Id == id);
            person.Name = "Explicit sync detection";

            Assert.Equal(EntityState.Unchanged, update.Entry(person).State);
            update.ChangeTracker.DetectChanges();
            Assert.Equal(EntityState.Modified, update.Entry(person).State);
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("Explicit sync detection", verify.People.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.Name)
            .Single());
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-AUTO-DETECT-SAVE-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AutoDetectChangesEnabled", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task SaveChangesAsync_DoesNotPersistUnmarkedChangesWhenAutomaticDetectionIsDisabled()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var person = await update.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
            update.ChangeTracker.AutoDetectChangesEnabled = false;
            person.Name = "Not detected asynchronously";

            Assert.Equal(EntityState.Unchanged, update.Entry(person).State);
            Assert.Equal(0, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.Equal(EntityState.Unchanged, update.Entry(person).State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal($"Tracked {id:N}", await verify.People.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-AUTO-DETECT-SAVE-SYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.AutoDetectChangesEnabled", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task SaveChanges_DoesNotPersistUnmarkedChangesWhenAutomaticDetectionIsDisabled()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var person = update.People.Single(item => item.Id == id);
            update.ChangeTracker.AutoDetectChangesEnabled = false;
            person.Name = "Not detected synchronously";

            Assert.Equal(EntityState.Unchanged, update.Entry(person).State);
            Assert.Equal(0, update.SaveChanges());
            Assert.Equal(EntityState.Unchanged, update.Entry(person).State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal($"Tracked {id:N}", verify.People.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.Name)
            .Single());
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-ENTRY-DETECT-CHANGES", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.DetectChanges", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.State", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task EntityEntryDetectChanges_FindsAndPersistsChangedProperty()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            update.ChangeTracker.AutoDetectChangesEnabled = false;
            var person = update.People.Single(item => item.Id == id);
            person.Age++;
            var entry = update.Entry(person);

            Assert.Equal(EntityState.Unchanged, entry.State);
            entry.DetectChanges();
            Assert.Equal(EntityState.Modified, entry.State);
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(51, verify.People.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.Age)
            .Single());
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-CLEAR", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.Clear", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task ChangeTrackerClear_DetachesEntitiesAndDiscardsUnsavedChanges()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            var person = await context.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
            person.Name = "Unpersisted value";

            context.ChangeTracker.Clear();

            Assert.Equal(EntityState.Detached, context.Entry(person).State);
            Assert.Equal(0, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal($"Tracked {id:N}", await verify.People.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-RELOAD-SYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.Reload", "Microsoft.EntityFrameworkCore.DbContext.Entry")]
    public async Task EntityEntryReload_RefreshesTrackedValuesSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var trackedContext = ProviderTestRuntime.CreateContext();
        var tracked = trackedContext.People.Single(person => person.Id == id);

        await using (var writer = ProviderTestRuntime.CreateContext())
        {
            var stored = writer.People.Single(person => person.Id == id);
            stored.Name = "Changed by another context";
            writer.SaveChanges();
        }

        trackedContext.Entry(tracked).Reload();

        Assert.Equal("Changed by another context", tracked.Name);
        Assert.Equal(EntityState.Unchanged, trackedContext.Entry(tracked).State);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-RELOAD-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.ReloadAsync", "Microsoft.EntityFrameworkCore.DbContext.Entry")]
    public async Task EntityEntryReloadAsync_RefreshesTrackedValues()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using var trackedContext = ProviderTestRuntime.CreateContext();
        var tracked = await trackedContext.People.SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);

        await using (var writer = ProviderTestRuntime.CreateContext())
        {
            var stored = await writer.People.SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);
            stored.Name = "Changed asynchronously by another context";
            await writer.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await trackedContext.Entry(tracked).ReloadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Changed asynchronously by another context", tracked.Name);
        Assert.Equal(EntityState.Unchanged, trackedContext.Entry(tracked).State);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-PROPERTY-ENTRY-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry`1.Property", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry.OriginalValue", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry`2.CurrentValue", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry.IsModified", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task PropertyEntry_ExposesOriginalAndModifiedValuesAsynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            var person = await context.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
            var age = context.Entry(person).Property(item => item.Age);

            Assert.Equal(50, age.OriginalValue);
            age.CurrentValue = 37;
            Assert.True(age.IsModified);
            Assert.Equal(37, age.CurrentValue);
            Assert.Equal(1, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(37, await verify.People.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.Age)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-PROPERTY-ENTRY-SYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry`1.Property", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry.OriginalValue", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry`2.CurrentValue", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry.IsModified", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task PropertyEntry_ExposesOriginalAndModifiedValuesSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            var person = context.People.Single(item => item.Id == id);
            var age = context.Entry(person).Property(item => item.Age);

            Assert.Equal(50, age.OriginalValue);
            age.CurrentValue = 38;
            Assert.True(age.IsModified);
            Assert.Equal(38, age.CurrentValue);
            Assert.Equal(1, context.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(38, verify.People.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => item.Age)
            .Single());
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-PROPERTY-VALUES-SET", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.CurrentValues", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues.SetValues", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues.TryGetValue", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues.Clone", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues.ToObject", "Microsoft.EntityFrameworkCore.ChangeTracking.PropertyValues.Item", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry.Property", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task PropertyValues_SetValuesAndSnapshotHelpersPreserveMappedValues()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var person = update.People.Single(item => item.Id == id);
            var entry = update.Entry(person);
            var values = entry.CurrentValues;
            Assert.Equal(person.Name, values[nameof(Person.Name)]);

            values.SetValues(new Person { Id = id, Name = "Set through PropertyValues", Age = 56 });

            Assert.True(entry.Property(nameof(Person.Age)).IsModified);
            Assert.True(values.TryGetValue<int>(nameof(Person.Age), out var age));
            Assert.Equal(56, age);
            Assert.Equal("Set through PropertyValues", values.Clone().ToObject() is Person copy ? copy.Name : null);
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal("Set through PropertyValues", actual.Name);
        Assert.Equal(56, actual.Age);
    }

    private static async Task SeedAsync(Guid id)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = $"Tracked {id:N}", Age = 50 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task UpdateNameAsync(Guid id, string name)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        var person = await context.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);
        person.Name = name;
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> SeedPostWithTwoCommentsAsync()
    {
        var post = new Post
        {
            Id = Guid.NewGuid(),
            Title = "Identity resolution",
            Comments =
            [
                new Comment { Id = Guid.NewGuid(), Text = "First" },
                new Comment { Id = Guid.NewGuid(), Text = "Second" }
            ]
        };
        var blog = new Blog { Id = Guid.NewGuid(), Name = $"Identity {Guid.NewGuid():N}", Posts = [post] };

        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.Blogs.Add(blog);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return post.Id;
    }
}
