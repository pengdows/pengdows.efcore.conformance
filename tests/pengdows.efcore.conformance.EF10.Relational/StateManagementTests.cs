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

    private static async Task SeedAsync(Guid id)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = $"Tracked {id:N}", Age = 50 });
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
