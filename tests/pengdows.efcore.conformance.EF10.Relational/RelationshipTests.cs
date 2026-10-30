using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class RelationshipTests
{
    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-INCLUDE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ThenInclude", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task IncludeThenInclude_LoadsRelatedEntitiesAsynchronously()
    {
        var blog = await CreateBlogAsync();

        await using var verify = ProviderTestRuntime.CreateContext();
        var loaded = await verify.Blogs
            .Include(item => item.Posts)
            .ThenInclude(post => post.Comments)
            .SingleAsync(item => item.Id == blog.Id, TestContext.Current.CancellationToken);

        Assert.Equal(blog.Name, loaded.Name);
        Assert.Equal(new[] { "First", "Second" }, loaded.Posts.Select(post => post.Title).Order().ToArray());
        Assert.All(loaded.Posts, post => Assert.Single(post.Comments));
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-INCLUDE-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ThenInclude", "System.Linq.Queryable.Single")]
    public async Task IncludeThenInclude_LoadsRelatedEntitiesSynchronously()
    {
        var blog = await CreateBlogAsync();

        await using var verify = ProviderTestRuntime.CreateContext();
        var loaded = verify.Blogs
            .Include(item => item.Posts)
            .ThenInclude(post => post.Comments)
            .Single(item => item.Id == blog.Id);

        Assert.Equal(blog.Name, loaded.Name);
        Assert.Equal(new[] { "First", "Second" }, loaded.Posts.Select(post => post.Title).Order().ToArray());
        Assert.All(loaded.Posts, post => Assert.Single(post.Comments));
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-FILTERED-INCLUDE-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "System.Linq.Queryable.Where", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ThenInclude", "System.Linq.Queryable.Single")]
    public async Task FilteredInclude_LoadsOnlyMatchingRelatedRowsSynchronously()
    {
        var blog = await CreateBlogAsync();

        await using (var query = ProviderTestRuntime.CreateContext())
        {
            var loaded = query.Blogs
                .Include(item => item.Posts.Where(post => post.Title == "First"))
                .ThenInclude(post => post.Comments)
                .Single(item => item.Id == blog.Id);

            var post = Assert.Single(loaded.Posts);
            Assert.Equal("First", post.Title);
            Assert.Equal("First comment", Assert.Single(post.Comments).Text);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(2, verify.Set<Post>().Count(post => post.BlogId == blog.Id));
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-FILTERED-INCLUDE-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "System.Linq.Queryable.Where", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ThenInclude", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task FilteredInclude_LoadsOnlyMatchingRelatedRowsAsynchronously()
    {
        var blog = await CreateBlogAsync();

        await using (var query = ProviderTestRuntime.CreateContext())
        {
            var loaded = await query.Blogs
                .Include(item => item.Posts.Where(post => post.Title == "First"))
                .ThenInclude(post => post.Comments)
                .SingleAsync(item => item.Id == blog.Id, TestContext.Current.CancellationToken);

            var post = Assert.Single(loaded.Posts);
            Assert.Equal("First", post.Title);
            Assert.Equal("First comment", Assert.Single(post.Comments).Text);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(2, await verify.Set<Post>().CountAsync(
            post => post.BlogId == blog.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-FOREIGN-KEY", "Microsoft.EntityFrameworkCore.DbSet`1.Add", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task ForeignKeyAndNavigation_PersistAndReadBack()
    {
        var blog = await CreateBlogAsync();
        var expected = blog.Posts.Single(post => post.Title == "First");

        await using var verify = ProviderTestRuntime.CreateContext();
        var post = await verify.Set<Post>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == expected.Id, TestContext.Current.CancellationToken);

        Assert.Equal(blog.Id, post.BlogId);
        Assert.Equal(expected.Title, post.Title);
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-SELECTMANY-ASYNC", "System.Linq.Queryable.SelectMany", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task SelectMany_FlattensRelatedPostsAsynchronously()
    {
        var blog = await CreateBlogAsync();

        await using var context = ProviderTestRuntime.CreateContext();
        var titles = await context.Blogs
            .Where(item => item.Id == blog.Id)
            .SelectMany(item => item.Posts)
            .Select(post => post.Title)
            .OrderBy(title => title)
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "First", "Second" }, titles);
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-SELECTMANY-SYNC", "System.Linq.Queryable.SelectMany", "System.Linq.Enumerable.ToList")]
    public async Task SelectMany_FlattensRelatedPostsSynchronously()
    {
        var blog = await CreateBlogAsync();

        await using var context = ProviderTestRuntime.CreateContext();
        var titles = context.Blogs
            .Where(item => item.Id == blog.Id)
            .SelectMany(item => item.Posts)
            .Select(post => post.Title)
            .OrderBy(title => title)
            .ToList();

        Assert.Equal(new[] { "First", "Second" }, titles);
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-SPLIT-QUERY-ASYNC", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.AsSplitQuery", "Microsoft.EntityFrameworkCore.QuerySplittingBehavior", "Microsoft.EntityFrameworkCore.QuerySplittingBehavior.SplitQuery", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ThenInclude", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task AsSplitQuery_MaterializesCompleteNavigationGraphAsynchronously()
    {
        var blog = await CreateBlogAsync();

        await using var context = ProviderTestRuntime.CreateContext();
        var loaded = await context.Blogs
            .AsSplitQuery()
            .Include(item => item.Posts)
            .ThenInclude(post => post.Comments)
            .SingleAsync(item => item.Id == blog.Id, TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "First", "Second" }, loaded.Posts.Select(post => post.Title).Order().ToArray());
        Assert.All(loaded.Posts, post => Assert.Single(post.Comments));
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-SINGLE-QUERY-SYNC", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.AsSingleQuery", "Microsoft.EntityFrameworkCore.QuerySplittingBehavior", "Microsoft.EntityFrameworkCore.QuerySplittingBehavior.SingleQuery", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ThenInclude", "System.Linq.Queryable.Single")]
    public async Task AsSingleQuery_MaterializesCompleteNavigationGraphSynchronously()
    {
        var blog = await CreateBlogAsync();

        await using var context = ProviderTestRuntime.CreateContext();
        var loaded = context.Blogs
            .AsSingleQuery()
            .Include(item => item.Posts)
            .ThenInclude(post => post.Comments)
            .Single(item => item.Id == blog.Id);

        Assert.Equal(new[] { "First", "Second" }, loaded.Posts.Select(post => post.Title).Order().ToArray());
        Assert.All(loaded.Posts, post => Assert.Single(post.Comments));
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-EXPLICIT-LOAD-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.CollectionEntry.LoadAsync", "Microsoft.EntityFrameworkCore.ChangeTracking.NavigationEntry.IsLoaded", "Microsoft.EntityFrameworkCore.ChangeTracking.ReferenceEntry.LoadAsync")]
    public async Task NavigationEntryLoadAsync_LoadsCollectionAndReference()
    {
        var blog = await CreateBlogAsync();

        await using (var collectionContext = ProviderTestRuntime.CreateContext())
        {
            var trackedBlog = await collectionContext.Blogs
                .SingleAsync(item => item.Id == blog.Id, TestContext.Current.CancellationToken);
            var posts = collectionContext.Entry(trackedBlog).Collection(item => item.Posts);

            Assert.False(posts.IsLoaded);
            await posts.LoadAsync(TestContext.Current.CancellationToken);
            Assert.True(posts.IsLoaded);
            Assert.Equal(new[] { "First", "Second" }, trackedBlog.Posts.Select(post => post.Title).Order().ToArray());
        }

        await using var referenceContext = ProviderTestRuntime.CreateContext();
        var trackedPost = await referenceContext.Set<Post>()
            .SingleAsync(item => item.Id == blog.Posts.First().Id, TestContext.Current.CancellationToken);
        var reference = referenceContext.Entry(trackedPost).Reference(item => item.Blog);

        Assert.False(reference.IsLoaded);
        await reference.LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(reference.IsLoaded);
        Assert.Equal(blog.Id, trackedPost.Blog!.Id);
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-EXPLICIT-LOAD-SYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.CollectionEntry.Load", "Microsoft.EntityFrameworkCore.ChangeTracking.NavigationEntry.IsLoaded", "Microsoft.EntityFrameworkCore.ChangeTracking.ReferenceEntry.Load")]
    public async Task NavigationEntryLoad_LoadsCollectionAndReferenceSynchronously()
    {
        var blog = await CreateBlogAsync();

        await using (var collectionContext = ProviderTestRuntime.CreateContext())
        {
            var trackedBlog = collectionContext.Blogs.Single(item => item.Id == blog.Id);
            var posts = collectionContext.Entry(trackedBlog).Collection(item => item.Posts);

            Assert.False(posts.IsLoaded);
            posts.Load();
            Assert.True(posts.IsLoaded);
            Assert.Equal(new[] { "First", "Second" }, trackedBlog.Posts.Select(post => post.Title).Order().ToArray());
        }

        await using var referenceContext = ProviderTestRuntime.CreateContext();
        var trackedPost = referenceContext.Set<Post>().Single(item => item.Id == blog.Posts.First().Id);
        var reference = referenceContext.Entry(trackedPost).Reference(item => item.Blog);

        Assert.False(reference.IsLoaded);
        reference.Load();
        Assert.True(reference.IsLoaded);
        Assert.Equal(blog.Id, trackedPost.Blog!.Id);
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-COLLECTION-QUERY-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.CollectionEntry.Query", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.LoadAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync")]
    public async Task CollectionEntryQueryAsync_LoadsOnlyMatchingRelatedRows()
    {
        var blog = await CreateBlogAsync();

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            var trackedBlog = await context.Blogs.SingleAsync(
                item => item.Id == blog.Id,
                TestContext.Current.CancellationToken);
            var posts = context.Entry(trackedBlog).Collection(item => item.Posts);

            await posts.Query()
                .Where(post => post.Title == "First")
                .LoadAsync(TestContext.Current.CancellationToken);

            Assert.Equal(new[] { "First" }, trackedBlog.Posts.Select(post => post.Title).ToArray());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(2, await verify.Set<Post>().CountAsync(
            post => post.BlogId == blog.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-COLLECTION-QUERY-SYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.CollectionEntry.Query", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Load", "System.Linq.Queryable.Count")]
    public async Task CollectionEntryQuery_LoadsOnlyMatchingRelatedRowsSynchronously()
    {
        var blog = await CreateBlogAsync();

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            var trackedBlog = context.Blogs.Single(item => item.Id == blog.Id);
            var posts = context.Entry(trackedBlog).Collection(item => item.Posts);

            posts.Query()
                .Where(post => post.Title == "First")
                .Load();

            Assert.Equal(new[] { "First" }, trackedBlog.Posts.Select(post => post.Title).ToArray());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(2, verify.Set<Post>().Count(post => post.BlogId == blog.Id));
    }

    private static async Task<Blog> CreateBlogAsync()
    {
        var blog = new Blog
        {
            Id = Guid.NewGuid(),
            Name = $"Blog {Guid.NewGuid():N}",
            Posts =
            [
                new Post
                {
                    Id = Guid.NewGuid(),
                    Title = "First",
                    Comments = [new Comment { Id = Guid.NewGuid(), Text = "First comment" }]
                },
                new Post
                {
                    Id = Guid.NewGuid(),
                    Title = "Second",
                    Comments = [new Comment { Id = Guid.NewGuid(), Text = "Second comment" }]
                }
            ]
        };

        await using var write = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
        write.Blogs.Add(blog);
        await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        return blog;
    }
}
