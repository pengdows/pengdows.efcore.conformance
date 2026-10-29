using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

internal static class ProviderTestRuntime
{
    private static readonly Lazy<IProviderAdapter> LoadedAdapter = new(LoadAdapter);

    public static IProviderAdapter Adapter
    {
        get
        {
            var adapter = LoadedAdapter.Value;
            adapter.Capabilities.Validate();
            return adapter;
        }
    }

    public static string ConnectionString => RequiredEnvironmentVariable("PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING");

    public static ConformanceDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ConformanceDbContext>();
        Adapter.Configure(options, ConnectionString);
        return new ConformanceDbContext(options.Options);
    }

    public static Task InitializeAsync(ConformanceDbContext context, CancellationToken cancellationToken) =>
        Adapter.InitializeDatabaseAsync(context, cancellationToken);

    public static void RequireCapability(string feature)
    {
        var capabilities = Adapter.Capabilities;
        if (capabilities.Supports(feature))
        {
            return;
        }

        if (capabilities.UnsupportedFeatures.Contains(feature))
        {
            Assert.Skip($"UNSUPPORTED: adapter declares that '{feature}' is not supported.");
        }

        Assert.Skip($"SKIP: adapter did not declare applicability for '{feature}'.");
    }

    private static IProviderAdapter LoadAdapter()
    {
        var assemblyPath = RequiredEnvironmentVariable("PENGDOWS_EFCORE_ADAPTER_ASSEMBLY");
        var typeName = RequiredEnvironmentVariable("PENGDOWS_EFCORE_ADAPTER_TYPE");
        return ProviderAdapterLoader.Load(assemblyPath, typeName);
    }

    private static string RequiredEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set {name} to run provider conformance tests.");
}

internal sealed class ConformanceDbContext(DbContextOptions<ConformanceDbContext> options) : DbContext(options)
{
    public DbSet<Person> People => Set<Person>();

    public DbSet<Blog> Blogs => Set<Blog>();

    public DbSet<ScalarRecord> ScalarRecords => Set<ScalarRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Person>(entity =>
        {
            entity.ToTable("ef10conformancepeople");
            entity.HasKey(person => person.Id);
            entity.Property(person => person.Id).HasColumnName("id");
            entity.Property(person => person.Name).HasColumnName("name").IsRequired().HasMaxLength(160);
            entity.Property(person => person.Age).HasColumnName("age");
            entity.Property(person => person.Version).HasColumnName("version").IsConcurrencyToken();
            entity.Property(person => person.IsDeleted).HasColumnName("isdeleted");
            entity.Property(person => person.IsArchived).HasColumnName("isarchived");
            entity.HasQueryFilter("SoftDelete", person => !person.IsDeleted);
            entity.HasQueryFilter("Archive", person => !person.IsArchived);
        });
        modelBuilder.Entity<Blog>(entity =>
        {
            entity.ToTable("ef10conformanceblogs");
            entity.HasKey(blog => blog.Id);
            entity.Property(blog => blog.Id).HasColumnName("id");
            entity.Property(blog => blog.Name).HasColumnName("name").IsRequired().HasMaxLength(160);
            entity.HasMany(blog => blog.Posts).WithOne(post => post.Blog).HasForeignKey(post => post.BlogId);
        });
        modelBuilder.Entity<Post>(entity =>
        {
            entity.ToTable("ef10conformanceposts");
            entity.HasKey(post => post.Id);
            entity.Property(post => post.Id).HasColumnName("id");
            entity.Property(post => post.BlogId).HasColumnName("blogid");
            entity.Property(post => post.Title).HasColumnName("title").IsRequired().HasMaxLength(160);
            entity.HasMany(post => post.Comments).WithOne(comment => comment.Post).HasForeignKey(comment => comment.PostId);
        });
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("ef10conformancecomments");
            entity.HasKey(comment => comment.Id);
            entity.Property(comment => comment.Id).HasColumnName("id");
            entity.Property(comment => comment.PostId).HasColumnName("postid");
            entity.Property(comment => comment.Text).HasColumnName("text").IsRequired().HasMaxLength(160);
        });
        modelBuilder.Entity<ScalarRecord>(entity =>
        {
            entity.ToTable("ef10conformancescalars");
            entity.HasKey(record => record.Id);
            entity.Property(record => record.Id).HasColumnName("id");
            entity.Property(record => record.Amount).HasColumnName("amount").HasColumnType("decimal(18,4)");
            entity.Property(record => record.Day).HasColumnName("day");
            entity.Property(record => record.Start).HasColumnName("start");
            entity.Property(record => record.Enabled).HasColumnName("enabled");
            entity.Property(record => record.Payload).HasColumnName("payload");
            entity.Property(record => record.Mood).HasColumnName("mood").HasConversion<string>();
        });
    }
}

internal sealed class Person
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Age { get; set; }

    public int Version { get; set; }

    public bool IsDeleted { get; set; }

    public bool IsArchived { get; set; }
}

internal sealed class Blog
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<Post> Posts { get; set; } = new List<Post>();
}

internal sealed class Post
{
    public Guid Id { get; set; }

    public Guid BlogId { get; set; }

    public string Title { get; set; } = string.Empty;

    public Blog? Blog { get; set; }

    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
}

internal sealed class Comment
{
    public Guid Id { get; set; }

    public Guid PostId { get; set; }

    public string Text { get; set; } = string.Empty;

    public Post? Post { get; set; }
}

internal sealed class ScalarRecord
{
    public Guid Id { get; set; }

    public decimal Amount { get; set; }

    public DateOnly Day { get; set; }

    public TimeOnly Start { get; set; }

    public bool Enabled { get; set; }

    public byte[] Payload { get; set; } = [];

    public TestMood Mood { get; set; }
}

internal enum TestMood
{
    Calm,
    Curious
}
