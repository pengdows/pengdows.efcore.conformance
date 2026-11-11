using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class ChangeTrackerGraphTests
{
    [Fact]
    [BehavioralCoverage("EF10-TRACKING-TRACK-GRAPH", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.TrackGraph", "System.Void TrackGraph(System.Object rootEntity, System.Action<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntryGraphNode> callback)", "System.Void TrackGraph<TState>(System.Object rootEntity, TState state, System.Func<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntryGraphNode<TState>, System.Boolean> callback)", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.Entries", "System.Collections.Generic.IEnumerable<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry> Entries()", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ThenInclude")]
    public async Task TrackGraph_TracksAndPersistsDisconnectedGraph()
    {
        var blog = new Blog
        {
            Id = Guid.NewGuid(),
            Name = "Tracked disconnected graph",
            Posts =
            [
                new Post
                {
                    Id = Guid.NewGuid(),
                    Title = "Graph post",
                    Comments = [new Comment { Id = Guid.NewGuid(), Text = "Graph comment" }]
                }
            ]
        };

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
            context.ChangeTracker.TrackGraph(blog, node => node.Entry.State = EntityState.Added);

            Assert.Equal(3, context.ChangeTracker.Entries().Count());
            Assert.Equal(3, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        var statefulBlog = new Blog
        {
            Id = Guid.NewGuid(),
            Name = "Stateful tracked disconnected graph",
            Posts =
            [
                new Post
                {
                    Id = Guid.NewGuid(),
                    Title = "Stateful graph post"
                }
            ]
        };

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            context.ChangeTracker.TrackGraph(statefulBlog, "added", node =>
            {
                Assert.Equal("added", node.NodeState);
                node.Entry.State = EntityState.Added;
                return node.Entry.Entity is Blog;
            });

            Assert.Equal(2, context.ChangeTracker.Entries().Count());
            Assert.Equal(2, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var statefulVerify = ProviderTestRuntime.CreateContext();
        var statefulActual = await statefulVerify.Blogs
            .Include(item => item.Posts)
            .ThenInclude(post => post.Comments)
            .SingleAsync(item => item.Id == statefulBlog.Id, TestContext.Current.CancellationToken);
        Assert.Equal("Stateful graph post", Assert.Single(statefulActual.Posts).Title);

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.Blogs
            .Include(item => item.Posts)
            .ThenInclude(post => post.Comments)
            .SingleAsync(item => item.Id == blog.Id, TestContext.Current.CancellationToken);
        Assert.Equal("Graph post", Assert.Single(actual.Posts).Title);
        Assert.Equal("Graph comment", Assert.Single(actual.Posts.Single().Comments).Text);
    }

    [Fact]
    [BehavioralCoverage("EF10-TRACKING-CASCADE-CHANGES", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.CascadeChanges", "Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker.CascadeDeleteTiming", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task CascadeChanges_AppliesDeferredDeleteCascade()
    {
        var blog = new Blog
        {
            Id = Guid.NewGuid(),
            Name = "Deferred cascade graph",
            Posts =
            [
                new Post
                {
                    Id = Guid.NewGuid(),
                    Title = "Deferred cascade post",
                    Comments = [new Comment { Id = Guid.NewGuid(), Text = "Deferred cascade comment" }]
                }
            ]
        };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.Blogs.Add(blog);
            await seed.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            var tracked = await delete.Blogs
                .Include(item => item.Posts)
                .ThenInclude(post => post.Comments)
                .SingleAsync(item => item.Id == blog.Id, TestContext.Current.CancellationToken);
            delete.ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
            delete.Blogs.Remove(tracked);

            Assert.Equal(EntityState.Unchanged, delete.ChangeTracker.Entries<Post>().Single().State);
            Assert.Equal(EntityState.Unchanged, delete.ChangeTracker.Entries<Comment>().Single().State);

            delete.ChangeTracker.CascadeChanges();

            Assert.Equal(EntityState.Deleted, delete.ChangeTracker.Entries<Post>().Single().State);
            Assert.Equal(EntityState.Deleted, delete.ChangeTracker.Entries<Comment>().Single().State);
            Assert.Equal(3, await delete.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.Blogs.AnyAsync(item => item.Id == blog.Id, TestContext.Current.CancellationToken));
    }
}
