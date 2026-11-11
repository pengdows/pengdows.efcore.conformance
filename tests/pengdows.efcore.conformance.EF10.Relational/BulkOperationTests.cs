using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class BulkOperationTests
{
    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync")]
    public async Task ExecuteUpdateAsync_ChangesMatchingRows()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var id = await SeedAsync();

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = await update.People.SingleAsync(
                person => person.Id == id,
                TestContext.Current.CancellationToken);
            var affected = await update.People.Where(person => person.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(person => person.Age, person => person.Age + 1),
                    TestContext.Current.CancellationToken);
            Assert.Equal(1, affected);
            Assert.Equal(40, tracked.Age);
            Assert.Equal(EntityState.Unchanged, update.Entry(tracked).State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(41, actual.Age);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdate")]
    public async Task ExecuteUpdate_ChangesMatchingRowsSynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var id = await SeedAsync();

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = update.People.Single(person => person.Id == id);
            var affected = update.People.Where(person => person.Id == id)
                .ExecuteUpdate(setters => setters.SetProperty(person => person.Age, person => person.Age + 1));
            Assert.Equal(1, affected);
            Assert.Equal(40, tracked.Age);
            Assert.Equal(EntityState.Unchanged, update.Entry(tracked).State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.People.AsNoTracking().Single(person => person.Id == id);
        Assert.Equal(41, actual.Age);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-MULTIPLE-PROPERTIES-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync")]
    public async Task ExecuteUpdateAsync_ChangesMultipleProperties()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var id = await SeedAsync();

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var affected = await update.People.Where(person => person.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(person => person.Name, person => person.Name + " updated")
                        .SetProperty(person => person.Age, person => person.Age + 2),
                    TestContext.Current.CancellationToken);
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal($"Bulk {id:N} updated", actual.Name);
        Assert.Equal(42, actual.Age);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-NO-MATCH-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync")]
    public async Task ExecuteUpdateAsync_ReturnsZeroWhenNoRowsMatch()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var missingId = Guid.NewGuid();
        await using var context = ProviderTestRuntime.CreateContext();

        var affected = await context.People
            .Where(person => person.Id == missingId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(person => person.Age, 50),
                TestContext.Current.CancellationToken);

        Assert.Equal(0, affected);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-MULTIPLE-PROPERTIES-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdate")]
    public async Task ExecuteUpdate_ChangesMultiplePropertiesSynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var id = await SeedAsync();

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var affected = update.People.Where(person => person.Id == id)
                .ExecuteUpdate(
                    setters => setters
                        .SetProperty(person => person.Name, person => person.Name + " sync updated")
                        .SetProperty(person => person.Age, person => person.Age + 3));
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.People.AsNoTracking().Single(person => person.Id == id);
        Assert.Equal($"Bulk {id:N} sync updated", actual.Name);
        Assert.Equal(43, actual.Age);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-ACTION-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync", "Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder.SetProperty")]
    public async Task ExecuteUpdateAsync_BuildsSettersWithConditionalAction()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var id = await SeedAsync();
        const bool updateName = true;
        const string updatedName = "Conditional async setter";

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var affected = await update.People.Where(person => person.Id == id)
                .ExecuteUpdateAsync(setters =>
                {
                    setters.SetProperty(person => person.Age, 47);
                    if (updateName)
                    {
                        setters.SetProperty(person => person.Name, updatedName);
                    }
                }, TestContext.Current.CancellationToken);
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal(updatedName, actual.Name);
        Assert.Equal(47, actual.Age);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-ACTION-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdate", "Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder.SetProperty")]
    public async Task ExecuteUpdate_BuildsSettersWithConditionalActionSynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var id = await SeedAsync();
        var updateName = false;

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var affected = update.People.Where(person => person.Id == id)
                .ExecuteUpdate(setters =>
                {
                    setters.SetProperty(person => person.Age, 48);
                    if (updateName)
                    {
                        setters.SetProperty(person => person.Name, "This name must remain unchanged");
                    }
                });
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.People.AsNoTracking()
            .SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);
        Assert.Equal($"Bulk {id:N}", actual.Name);
        Assert.Equal(48, actual.Age);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-NULL-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync")]
    public async Task ExecuteUpdateAsync_SetsNullablePropertyToNull()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var id = await SeedAsync(optionalRank: 8);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = await update.People.SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);
            Assert.Equal(8, tracked.OptionalRank);
            Assert.Equal(1, await update.People.Where(person => person.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(person => person.OptionalRank, (int?)null),
                    TestContext.Current.CancellationToken));
            Assert.Equal(8, tracked.OptionalRank);
            Assert.Equal(EntityState.Unchanged, update.Entry(tracked).State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Null(await verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.OptionalRank)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-NULL-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdate")]
    public async Task ExecuteUpdate_SetsNullablePropertyToNullSynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var id = await SeedAsync(optionalRank: 8);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = update.People.Single(person => person.Id == id);
            Assert.Equal(8, tracked.OptionalRank);
            Assert.Equal(1, update.People.Where(person => person.Id == id)
                .ExecuteUpdate(setters => setters.SetProperty(person => person.OptionalRank, (int?)null)));
            Assert.Equal(8, tracked.OptionalRank);
            Assert.Equal(EntityState.Unchanged, update.Entry(tracked).State);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Null(verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.OptionalRank)
            .Single());
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-NO-MATCH-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdate")]
    public async Task ExecuteUpdate_ReturnsZeroWhenNoRowsMatchSynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var missingId = Guid.NewGuid();
        await using var context = ProviderTestRuntime.CreateContext();

        var affected = context.People
            .Where(person => person.Id == missingId)
            .ExecuteUpdate(setters => setters.SetProperty(person => person.Age, 50));

        Assert.Equal(0, affected);
    }

    [Fact]
    [CapabilityRequirement("execute-delete")]
    [BehavioralCoverage("EF10-BULK-DELETE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDeleteAsync")]
    public async Task ExecuteDeleteAsync_RemovesMatchingRows()
    {
        ProviderTestRuntime.RequireCapability("execute-delete");
        var id = await SeedAsync();

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var tracked = await delete.People.SingleAsync(person => person.Id == id, TestContext.Current.CancellationToken);
            Assert.Equal(1, await delete.People.Where(person => person.Id == id)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken));
            Assert.Equal(EntityState.Unchanged, delete.Entry(tracked).State);
            Assert.Equal(40, tracked.Age);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AnyAsync(person => person.Id == id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("execute-delete")]
    [BehavioralCoverage("EF10-BULK-DELETE-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDelete")]
    public async Task ExecuteDelete_RemovesMatchingRowsSynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-delete");
        var id = await SeedAsync();

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var tracked = delete.People.Single(person => person.Id == id);
            Assert.Equal(1, delete.People.Where(person => person.Id == id).ExecuteDelete());
            Assert.Equal(EntityState.Unchanged, delete.Entry(tracked).State);
            Assert.Equal(40, tracked.Age);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(verify.People.Any(person => person.Id == id));
    }

    [Fact]
    [CapabilityRequirement("execute-delete")]
    [BehavioralCoverage("EF10-BULK-DELETE-NO-MATCH-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDeleteAsync")]
    public async Task ExecuteDeleteAsync_ReturnsZeroWhenNoRowsMatch()
    {
        ProviderTestRuntime.RequireCapability("execute-delete");
        var missingId = Guid.NewGuid();
        await using var context = ProviderTestRuntime.CreateContext();

        var affected = await context.People
            .Where(person => person.Id == missingId)
            .ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, affected);
    }

    [Fact]
    [CapabilityRequirement("execute-delete")]
    [BehavioralCoverage("EF10-BULK-DELETE-NO-MATCH-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDelete")]
    public async Task ExecuteDelete_ReturnsZeroWhenNoRowsMatchSynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-delete");
        var missingId = Guid.NewGuid();
        await using var context = ProviderTestRuntime.CreateContext();

        var affected = context.People.Where(person => person.Id == missingId).ExecuteDelete();

        Assert.Equal(0, affected);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-NAVIGATION-PREDICATE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync", "System.Linq.Queryable.Any")]
    public async Task ExecuteUpdateAsync_FiltersThroughRelatedEntity()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var matchingBlog = new Blog { Id = Guid.NewGuid(), Name = "Bulk navigation target" };
        var otherBlog = new Blog { Id = Guid.NewGuid(), Name = "Other bulk blog" };
        var matchingPost = new Post { Id = Guid.NewGuid(), BlogId = matchingBlog.Id, Title = "Before matching update" };
        var otherPost = new Post { Id = Guid.NewGuid(), BlogId = otherBlog.Id, Title = "Before other update" };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.Blogs.AddRange(matchingBlog, otherBlog);
            seed.Set<Post>().AddRange(matchingPost, otherPost);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var affected = await update.Set<Post>()
                .Where(post => post.Blog!.Name == "Bulk navigation target")
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(post => post.Title, "Updated by related entity"),
                    TestContext.Current.CancellationToken);
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var results = await verify.Set<Post>().AsNoTracking()
            .Where(post => post.Id == matchingPost.Id || post.Id == otherPost.Id)
            .Select(post => new { post.Id, post.Title })
            .ToDictionaryAsync(post => post.Id, post => post.Title, TestContext.Current.CancellationToken);
        Assert.Equal("Updated by related entity", results[matchingPost.Id]);
        Assert.Equal("Before other update", results[otherPost.Id]);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-BULK-UPDATE-NAVIGATION-PREDICATE-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdate", "System.Linq.Queryable.Any")]
    public async Task ExecuteUpdate_FiltersThroughRelatedEntitySynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var matchingBlog = new Blog { Id = Guid.NewGuid(), Name = "Bulk sync navigation target" };
        var otherBlog = new Blog { Id = Guid.NewGuid(), Name = "Other sync bulk blog" };
        var matchingPost = new Post { Id = Guid.NewGuid(), BlogId = matchingBlog.Id, Title = "Before sync matching update" };
        var otherPost = new Post { Id = Guid.NewGuid(), BlogId = otherBlog.Id, Title = "Before sync other update" };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.Blogs.AddRange(matchingBlog, otherBlog);
            seed.Set<Post>().AddRange(matchingPost, otherPost);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var affected = update.Set<Post>()
                .Where(post => post.Blog!.Name == "Bulk sync navigation target")
                .ExecuteUpdate(setters => setters.SetProperty(post => post.Title, "Updated by sync related entity"));
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var results = verify.Set<Post>().AsNoTracking()
            .Where(post => post.Id == matchingPost.Id || post.Id == otherPost.Id)
            .Select(post => new { post.Id, post.Title })
            .ToDictionary(post => post.Id, post => post.Title);
        Assert.Equal("Updated by sync related entity", results[matchingPost.Id]);
        Assert.Equal("Before sync other update", results[otherPost.Id]);
    }

    [Fact]
    [CapabilityRequirement("execute-delete")]
    [BehavioralCoverage("EF10-BULK-DELETE-NAVIGATION-PREDICATE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDeleteAsync", "System.Linq.Queryable.Any")]
    public async Task ExecuteDeleteAsync_FiltersThroughRelatedEntity()
    {
        ProviderTestRuntime.RequireCapability("execute-delete");
        var matchingBlog = new Blog { Id = Guid.NewGuid(), Name = "Bulk navigation delete target" };
        var otherBlog = new Blog { Id = Guid.NewGuid(), Name = "Other bulk delete blog" };
        var matchingPost = new Post { Id = Guid.NewGuid(), BlogId = matchingBlog.Id, Title = "Delete matching post" };
        var otherPost = new Post { Id = Guid.NewGuid(), BlogId = otherBlog.Id, Title = "Keep other post" };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.Blogs.AddRange(matchingBlog, otherBlog);
            seed.Set<Post>().AddRange(matchingPost, otherPost);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var affected = await delete.Set<Post>()
                .Where(post => post.Blog!.Name == "Bulk navigation delete target")
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.Set<Post>().AnyAsync(post => post.Id == matchingPost.Id, TestContext.Current.CancellationToken));
        Assert.True(await verify.Set<Post>().AnyAsync(post => post.Id == otherPost.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("execute-delete")]
    [BehavioralCoverage("EF10-BULK-DELETE-NAVIGATION-PREDICATE-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteDelete", "System.Linq.Queryable.Any")]
    public async Task ExecuteDelete_FiltersThroughRelatedEntitySynchronously()
    {
        ProviderTestRuntime.RequireCapability("execute-delete");
        var matchingBlog = new Blog { Id = Guid.NewGuid(), Name = "Bulk sync navigation delete target" };
        var otherBlog = new Blog { Id = Guid.NewGuid(), Name = "Other sync bulk delete blog" };
        var matchingPost = new Post { Id = Guid.NewGuid(), BlogId = matchingBlog.Id, Title = "Delete sync matching post" };
        var otherPost = new Post { Id = Guid.NewGuid(), BlogId = otherBlog.Id, Title = "Keep sync other post" };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.Blogs.AddRange(matchingBlog, otherBlog);
            seed.Set<Post>().AddRange(matchingPost, otherPost);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var affected = delete.Set<Post>()
                .Where(post => post.Blog!.Name == "Bulk sync navigation delete target")
                .ExecuteDelete();
            Assert.Equal(1, affected);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(verify.Set<Post>().Any(post => post.Id == matchingPost.Id));
        Assert.True(verify.Set<Post>().Any(post => post.Id == otherPost.Id));
    }

    private static async Task<Guid> SeedAsync(int? optionalRank = null)
    {
        var id = Guid.NewGuid();
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = $"Bulk {id:N}", Age = 40, OptionalRank = optionalRank });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return id;
    }
}
