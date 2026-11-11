using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
    [BehavioralCoverage("EF10-RELATIONSHIPS-ONE-TO-ONE-SYNC", "Microsoft.EntityFrameworkCore.DbSet`1.Add", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "System.Linq.Queryable.Single")]
    public async Task OneToOneOptionalNavigation_PersistsAndLoadsSynchronously()
    {
        var configuredId = Guid.NewGuid();
        var emptyId = Guid.NewGuid();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.Accounts.AddRange(
                new Account
                {
                    Id = configuredId,
                    Name = "Configured account",
                    Settings = new AccountSettings { Value = "dark mode" }
                },
                new Account { Id = emptyId, Name = "Unconfigured account" });
            Assert.Equal(3, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var configured = verify.Accounts.Include(account => account.Settings).Single(account => account.Id == configuredId);
        var empty = verify.Accounts.Include(account => account.Settings).Single(account => account.Id == emptyId);
        Assert.Equal("dark mode", configured.Settings!.Value);
        Assert.Same(configured, configured.Settings.Account);
        Assert.Null(empty.Settings);
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-ONE-TO-ONE-ASYNC", "Microsoft.EntityFrameworkCore.DbSet`1.AddAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task OneToOneOptionalNavigation_PersistsAndLoadsAsynchronously()
    {
        var configuredId = Guid.NewGuid();
        var emptyId = Guid.NewGuid();
        var cancellationToken = TestContext.Current.CancellationToken;
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, cancellationToken);
            await write.Accounts.AddRangeAsync(
                [
                    new Account
                    {
                        Id = configuredId,
                        Name = "Configured async account",
                        Settings = new AccountSettings { Value = "high contrast" }
                    },
                    new Account { Id = emptyId, Name = "Unconfigured async account" }
                ],
                cancellationToken);
            Assert.Equal(3, await write.SaveChangesAsync(cancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var configured = await verify.Accounts.Include(account => account.Settings)
            .SingleAsync(account => account.Id == configuredId, cancellationToken);
        var empty = await verify.Accounts.Include(account => account.Settings)
            .SingleAsync(account => account.Id == emptyId, cancellationToken);
        Assert.Equal("high contrast", configured.Settings!.Value);
        Assert.Same(configured, configured.Settings.Account);
        Assert.Null(empty.Settings);
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-MANY-TO-MANY-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.CollectionCollectionBuilder.UsingEntity", "Microsoft.EntityFrameworkCore.DbSet`1.AddRange", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "System.Linq.Queryable.Single")]
    public async Task ManyToManySkipNavigations_PersistAndLoadBothDirectionsSynchronously()
    {
        var algebraId = Guid.NewGuid();
        var historyId = Guid.NewGuid();
        var adaId = Guid.NewGuid();
        var graceId = Guid.NewGuid();
        var algebra = new Course { Id = algebraId, Title = "Algebra" };
        var history = new Course { Id = historyId, Title = "History" };
        var ada = new Learner { Id = adaId, Name = "Ada" };
        var grace = new Learner { Id = graceId, Name = "Grace" };
        algebra.Learners.Add(ada);
        algebra.Learners.Add(grace);
        history.Learners.Add(ada);

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.Courses.AddRange(algebra, history);
            write.SaveChanges();
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var loadedAlgebra = verify.Courses.Include(course => course.Learners).Single(course => course.Id == algebraId);
        var loadedHistory = verify.Courses.Include(course => course.Learners).Single(course => course.Id == historyId);
        var loadedAda = verify.Learners.Include(learner => learner.Courses).Single(learner => learner.Id == adaId);

        Assert.Equal(new[] { "Ada", "Grace" }, loadedAlgebra.Learners.Select(learner => learner.Name).Order().ToArray());
        Assert.Equal(new[] { "Ada" }, loadedHistory.Learners.Select(learner => learner.Name).ToArray());
        Assert.Equal(new[] { "Algebra", "History" }, loadedAda.Courses.Select(course => course.Title).Order().ToArray());
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-MANY-TO-MANY-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.CollectionCollectionBuilder.UsingEntity", "Microsoft.EntityFrameworkCore.DbSet`1.AddRangeAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task ManyToManySkipNavigations_PersistAndLoadBothDirectionsAsynchronously()
    {
        var algebraId = Guid.NewGuid();
        var historyId = Guid.NewGuid();
        var adaId = Guid.NewGuid();
        var graceId = Guid.NewGuid();
        var algebra = new Course { Id = algebraId, Title = "Async Algebra" };
        var history = new Course { Id = historyId, Title = "Async History" };
        var ada = new Learner { Id = adaId, Name = "Async Ada" };
        var grace = new Learner { Id = graceId, Name = "Async Grace" };
        algebra.Learners.Add(ada);
        algebra.Learners.Add(grace);
        history.Learners.Add(ada);
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, cancellationToken);
            await write.Courses.AddRangeAsync([algebra, history], cancellationToken);
            await write.SaveChangesAsync(cancellationToken);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var loadedAlgebra = await verify.Courses.Include(course => course.Learners)
            .SingleAsync(course => course.Id == algebraId, cancellationToken);
        var loadedHistory = await verify.Courses.Include(course => course.Learners)
            .SingleAsync(course => course.Id == historyId, cancellationToken);
        var loadedAda = await verify.Learners.Include(learner => learner.Courses)
            .SingleAsync(learner => learner.Id == adaId, cancellationToken);

        Assert.Equal(new[] { "Async Ada", "Async Grace" }, loadedAlgebra.Learners.Select(learner => learner.Name).Order().ToArray());
        Assert.Equal(new[] { "Async Ada" }, loadedHistory.Learners.Select(learner => learner.Name).ToArray());
        Assert.Equal(new[] { "Async Algebra", "Async History" }, loadedAda.Courses.Select(course => course.Title).Order().ToArray());
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
    [BehavioralCoverage("EF10-SPLIT-QUERY-ORDERING-ASYNC", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.AsSplitQuery", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.Take", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task AsSplitQuery_WithDuplicateSortKeysAndTakeMatchesSingleQueryAsynchronously()
    {
        var blogs = await CreateDuplicateSortBlogsAsync();

        await using var context = ProviderTestRuntime.CreateContext();
        var split = await context.Blogs.AsNoTracking()
            .Where(blog => blog.Name == blogs[0].Name)
            .OrderBy(blog => blog.Name)
            .Take(2)
            .AsSplitQuery()
            .Include(blog => blog.Posts)
            .ToListAsync(TestContext.Current.CancellationToken);

        var single = await context.Blogs.AsNoTracking()
            .Where(blog => blog.Name == blogs[0].Name)
            .OrderBy(blog => blog.Name)
            .Take(2)
            .AsSingleQuery()
            .Include(blog => blog.Posts)
            .ToListAsync(TestContext.Current.CancellationToken);

        AssertEquivalentBlogGraphs(blogs, split, single);
    }

    [Fact]
    [BehavioralCoverage("EF10-SPLIT-QUERY-ORDERING-SYNC", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.AsSplitQuery", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.Include", "System.Linq.Queryable.OrderBy", "System.Linq.Queryable.Take", "System.Linq.Enumerable.ToList")]
    public async Task AsSplitQuery_WithDuplicateSortKeysAndTakeMatchesSingleQuerySynchronously()
    {
        var blogs = await CreateDuplicateSortBlogsAsync();

        await using var context = ProviderTestRuntime.CreateContext();
        var split = context.Blogs.AsNoTracking()
            .Where(blog => blog.Name == blogs[0].Name)
            .OrderBy(blog => blog.Name)
            .Take(2)
            .AsSplitQuery()
            .Include(blog => blog.Posts)
            .ToList();

        var single = context.Blogs.AsNoTracking()
            .Where(blog => blog.Name == blogs[0].Name)
            .OrderBy(blog => blog.Name)
            .Take(2)
            .AsSingleQuery()
            .Include(blog => blog.Posts)
            .ToList();

        AssertEquivalentBlogGraphs(blogs, split, single);
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
    [BehavioralCoverage("EF10-DEFAULT-IF-EMPTY-CHILD-PRESERVES-PARENT-ASYNC", "System.Linq.Queryable.SelectMany", "System.Linq.Enumerable.DefaultIfEmpty", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task DefaultIfEmptyOnChildCollection_PreservesParentRowsAsynchronously()
    {
        var blogs = await CreateDefaultIfEmptyBlogsAsync();

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = await query.Blogs.AsNoTracking()
            .Where(blog => blogs.Select(seed => seed.Id).Contains(blog.Id))
            .SelectMany(blog => blog.Posts.DefaultIfEmpty(), (blog, post) => new ParentPostResult(blog.Id, blog.Name, post == null ? null : post.Title))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(
            new[]
            {
                new ParentPostResult(blogs[0].Id, blogs[0].Name, null),
                new ParentPostResult(blogs[1].Id, blogs[1].Name, "Child")
            }.OrderBy(result => result.ParentId).ToArray(),
            actual.OrderBy(result => result.ParentId).ToArray());
    }

    [Fact]
    [BehavioralCoverage("EF10-DEFAULT-IF-EMPTY-CHILD-PRESERVES-PARENT-SYNC", "System.Linq.Queryable.SelectMany", "System.Linq.Enumerable.DefaultIfEmpty", "System.Linq.Enumerable.ToArray")]
    public async Task DefaultIfEmptyOnChildCollection_PreservesParentRowsSynchronously()
    {
        var blogs = await CreateDefaultIfEmptyBlogsAsync();

        await using var query = ProviderTestRuntime.CreateContext();
        var actual = query.Blogs.AsNoTracking()
            .Where(blog => blogs.Select(seed => seed.Id).Contains(blog.Id))
            .SelectMany(blog => blog.Posts.DefaultIfEmpty(), (blog, post) => new ParentPostResult(blog.Id, blog.Name, post == null ? null : post.Title))
            .ToArray();

        Assert.Equal(
            new[]
            {
                new ParentPostResult(blogs[0].Id, blogs[0].Name, null),
                new ParentPostResult(blogs[1].Id, blogs[1].Name, "Child")
            }.OrderBy(result => result.ParentId).ToArray(),
            actual.OrderBy(result => result.ParentId).ToArray());
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-EXPLICIT-LOAD-ASYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.CollectionEntry.LoadAsync", "System.Threading.Tasks.Task LoadAsync(Microsoft.EntityFrameworkCore.ChangeTracking.LoadOptions options, System.Threading.CancellationToken cancellationToken = null)", "Microsoft.EntityFrameworkCore.ChangeTracking.NavigationEntry.IsLoaded", "Microsoft.EntityFrameworkCore.ChangeTracking.ReferenceEntry.LoadAsync", "System.Threading.Tasks.Task LoadAsync(Microsoft.EntityFrameworkCore.ChangeTracking.LoadOptions options, System.Threading.CancellationToken cancellationToken = null)")]
    public async Task NavigationEntryLoadAsync_LoadsCollectionAndReference()
    {
        var blog = await CreateBlogAsync();

        await using (var collectionContext = ProviderTestRuntime.CreateContext())
        {
            var trackedBlog = await collectionContext.Blogs
                .SingleAsync(item => item.Id == blog.Id, TestContext.Current.CancellationToken);
            var posts = collectionContext.Entry(trackedBlog).Collection(item => item.Posts);

            Assert.False(posts.IsLoaded);
            await posts.LoadAsync(LoadOptions.ForceIdentityResolution, TestContext.Current.CancellationToken);
            Assert.True(posts.IsLoaded);
            Assert.Equal(new[] { "First", "Second" }, trackedBlog.Posts.Select(post => post.Title).Order().ToArray());
        }

        await using var referenceContext = ProviderTestRuntime.CreateContext();
        var trackedPost = await referenceContext.Set<Post>()
            .SingleAsync(item => item.Id == blog.Posts.First().Id, TestContext.Current.CancellationToken);
        var reference = referenceContext.Entry(trackedPost).Reference(item => item.Blog);

        Assert.False(reference.IsLoaded);
        await reference.LoadAsync(LoadOptions.ForceIdentityResolution, TestContext.Current.CancellationToken);
        Assert.True(reference.IsLoaded);
        Assert.Equal(blog.Id, trackedPost.Blog!.Id);
    }

    [Fact]
    [BehavioralCoverage("EF10-RELATIONSHIPS-EXPLICIT-LOAD-SYNC", "Microsoft.EntityFrameworkCore.ChangeTracking.CollectionEntry.Load", "System.Void Load(Microsoft.EntityFrameworkCore.ChangeTracking.LoadOptions options)", "Microsoft.EntityFrameworkCore.ChangeTracking.NavigationEntry.IsLoaded", "Microsoft.EntityFrameworkCore.ChangeTracking.ReferenceEntry.Load", "System.Void Load(Microsoft.EntityFrameworkCore.ChangeTracking.LoadOptions options)")]
    public async Task NavigationEntryLoad_LoadsCollectionAndReferenceSynchronously()
    {
        var blog = await CreateBlogAsync();

        await using (var collectionContext = ProviderTestRuntime.CreateContext())
        {
            var trackedBlog = collectionContext.Blogs.Single(item => item.Id == blog.Id);
            var posts = collectionContext.Entry(trackedBlog).Collection(item => item.Posts);

            Assert.False(posts.IsLoaded);
            posts.Load(LoadOptions.ForceIdentityResolution);
            Assert.True(posts.IsLoaded);
            Assert.Equal(new[] { "First", "Second" }, trackedBlog.Posts.Select(post => post.Title).Order().ToArray());
        }

        await using var referenceContext = ProviderTestRuntime.CreateContext();
        var trackedPost = referenceContext.Set<Post>().Single(item => item.Id == blog.Posts.First().Id);
        var reference = referenceContext.Entry(trackedPost).Reference(item => item.Blog);

        Assert.False(reference.IsLoaded);
        reference.Load(LoadOptions.ForceIdentityResolution);
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

    private static async Task<Blog[]> CreateDuplicateSortBlogsAsync()
    {
        var sharedName = $"Duplicate sort {Guid.NewGuid():N}";
        var blogs = Enumerable.Range(0, 3).Select(index => new Blog
        {
            Id = Guid.NewGuid(),
            Name = sharedName,
            Posts = [new Post { Id = Guid.NewGuid(), Title = $"Post {index} A" }, new Post { Id = Guid.NewGuid(), Title = $"Post {index} B" }]
        }).ToArray();

        await using var write = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
        write.Blogs.AddRange(blogs);
        await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        return blogs;
    }

    private static async Task<Blog[]> CreateDefaultIfEmptyBlogsAsync()
    {
        var batch = Guid.NewGuid().ToString("N");
        var blogs = new[]
        {
            new Blog { Id = Guid.NewGuid(), Name = $"{batch}-empty" },
            new Blog { Id = Guid.NewGuid(), Name = $"{batch}-with-child", Posts = [new Post { Id = Guid.NewGuid(), Title = "Child" }] }
        };

        await using var write = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
        write.Blogs.AddRange(blogs);
        await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        return blogs;
    }

    private static void AssertEquivalentBlogGraphs(IReadOnlyCollection<Blog> seeded, IReadOnlyCollection<Blog> split, IReadOnlyCollection<Blog> single)
    {
        Assert.Equal(2, split.Count);
        Assert.Equal(2, single.Count);
        Assert.Equal(single.Select(blog => blog.Id).Order().ToArray(), split.Select(blog => blog.Id).Order().ToArray());
        foreach (var blog in split)
        {
            var expectedPosts = seeded.Single(seed => seed.Id == blog.Id).Posts.Select(post => post.Title).Order().ToArray();
            Assert.Equal(expectedPosts, blog.Posts.Select(post => post.Title).Order().ToArray());
        }

        foreach (var blog in single)
        {
            var expectedPosts = seeded.Single(seed => seed.Id == blog.Id).Posts.Select(post => post.Title).Order().ToArray();
            Assert.Equal(expectedPosts, blog.Posts.Select(post => post.Title).Order().ToArray());
        }
    }
}

internal sealed record ParentPostResult(Guid ParentId, string ParentName, string? ChildTitle);
