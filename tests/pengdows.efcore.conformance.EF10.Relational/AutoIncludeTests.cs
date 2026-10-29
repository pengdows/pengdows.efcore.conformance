using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class AutoIncludeTests
{
    [Fact]
    [BehavioralCoverage("EF10-QUERY-IGNORE-AUTO-INCLUDES-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreAutoIncludes", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task IgnoreAutoIncludesAsync_SuppressesConfiguredNavigationLoading()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var id = await SeedBlogAsync();

        await using (var context = CreateContext())
        {
            var included = await context.Blogs.SingleAsync(blog => blog.Id == id, TestContext.Current.CancellationToken);
            Assert.Single(included.Posts);
            Assert.Single(included.Posts.Single().Comments);
        }

        await using var ignoredContext = CreateContext();
        var ignored = await ignoredContext.Blogs
            .IgnoreAutoIncludes()
            .SingleAsync(blog => blog.Id == id, TestContext.Current.CancellationToken);

        Assert.Empty(ignored.Posts);
        Assert.Single(ignoredContext.ChangeTracker.Entries<Blog>());
        Assert.Empty(ignoredContext.ChangeTracker.Entries<Post>());
    }

    [Fact]
    [BehavioralCoverage("EF10-QUERY-IGNORE-AUTO-INCLUDES-SYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.IgnoreAutoIncludes", "System.Linq.Queryable.Single")]
    public async Task IgnoreAutoIncludes_SuppressesConfiguredNavigationLoadingSynchronously()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var id = await SeedBlogAsync();

        await using (var context = CreateContext())
        {
            var included = context.Blogs.Single(blog => blog.Id == id);
            Assert.Single(included.Posts);
            Assert.Single(included.Posts.Single().Comments);
        }

        await using var ignoredContext = CreateContext();
        var ignored = ignoredContext.Blogs
            .IgnoreAutoIncludes()
            .Single(blog => blog.Id == id);

        Assert.Empty(ignored.Posts);
        Assert.Single(ignoredContext.ChangeTracker.Entries<Blog>());
        Assert.Empty(ignoredContext.ChangeTracker.Entries<Post>());
    }

    private static async Task<Guid> SeedBlogAsync()
    {
        var blog = new Blog
        {
            Id = Guid.NewGuid(),
            Name = $"Auto include {Guid.NewGuid():N}",
            Posts =
            [
                new Post
                {
                    Id = Guid.NewGuid(),
                    Title = "Auto included post",
                    Comments = [new Comment { Id = Guid.NewGuid(), Text = "Auto included comment" }]
                }
            ]
        };

        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.Blogs.Add(blog);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return blog.Id;
    }

    private static AutoIncludeDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AutoIncludeDbContext>();
        ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString);
        return new AutoIncludeDbContext(options.Options);
    }
}

internal sealed class AutoIncludeDbContext(DbContextOptions<AutoIncludeDbContext> options) : DbContext(options)
{
    public DbSet<Blog> Blogs => Set<Blog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Blog>(entity =>
        {
            entity.ToTable("ef10conformanceblogs");
            entity.HasKey(blog => blog.Id);
            entity.Property(blog => blog.Id).HasColumnName("id");
            entity.Property(blog => blog.Name).HasColumnName("name").IsRequired().HasMaxLength(160);
            entity.HasMany(blog => blog.Posts).WithOne(post => post.Blog).HasForeignKey(post => post.BlogId);
            entity.Navigation(blog => blog.Posts).AutoInclude();
        });
        modelBuilder.Entity<Post>(entity =>
        {
            entity.ToTable("ef10conformanceposts");
            entity.HasKey(post => post.Id);
            entity.Property(post => post.Id).HasColumnName("id");
            entity.Property(post => post.BlogId).HasColumnName("blogid");
            entity.Property(post => post.Title).HasColumnName("title").IsRequired().HasMaxLength(160);
            entity.HasMany(post => post.Comments).WithOne(comment => comment.Post).HasForeignKey(comment => comment.PostId);
            entity.Navigation(post => post.Comments).AutoInclude();
        });
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("ef10conformancecomments");
            entity.HasKey(comment => comment.Id);
            entity.Property(comment => comment.Id).HasColumnName("id");
            entity.Property(comment => comment.PostId).HasColumnName("postid");
            entity.Property(comment => comment.Text).HasColumnName("text").IsRequired().HasMaxLength(400);
        });
    }
}
