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

    public static ConformanceDbContext CreateContext(Action<DbContextOptionsBuilder<ConformanceDbContext>>? configureOptions = null)
    {
        try
        {
            var options = new DbContextOptionsBuilder<ConformanceDbContext>();
            Adapter.Configure(options, ConnectionString);
            configureOptions?.Invoke(options);
            return new ConformanceDbContext(options.Options);
        }
        catch (Exception exception) when (exception is not ConformanceInfrastructureException)
        {
            throw new ConformanceInfrastructureException("Provider adapter configuration failed.", exception);
        }
    }

    public static async Task InitializeAsync(ConformanceDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            await Adapter.InitializeDatabaseAsync(context, cancellationToken);
        }
        catch (Exception exception) when (exception is not ConformanceInfrastructureException)
        {
            throw new ConformanceInfrastructureException("Provider database initialization failed.", exception);
        }
    }

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

    public DbSet<TphItem> TphItems => Set<TphItem>();

    public DbSet<TptItem> TptItems => Set<TptItem>();

    public DbSet<TpcItem> TpcItems => Set<TpcItem>();

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
        modelBuilder.Entity<TphItem>(entity =>
        {
            entity.ToTable("ef10conformancetph");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name").IsRequired();
            entity.HasDiscriminator<string>("kind")
                .HasValue<TphArticle>("article")
                .HasValue<TphVideo>("video");
        });
        modelBuilder.Entity<TptItem>(entity =>
        {
            entity.UseTptMappingStrategy();
            entity.ToTable("ef10conformancetptbase");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name").IsRequired();
        });
        modelBuilder.Entity<TptArticle>().ToTable("ef10conformancetptarticle");
        modelBuilder.Entity<TptVideo>().ToTable("ef10conformancetptvideo");
        modelBuilder.Entity<TpcItem>(entity =>
        {
            entity.UseTpcMappingStrategy();
            entity.HasKey(item => item.Id);
            entity.Property(item => item.Id).HasColumnName("id");
            entity.Property(item => item.Name).HasColumnName("name").IsRequired();
        });
        modelBuilder.Entity<TpcArticle>().ToTable("ef10conformancetpcarticle");
        modelBuilder.Entity<TpcVideo>().ToTable("ef10conformancetpcvideo");
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

internal abstract class TphItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class TphArticle : TphItem
{
    public string Body { get; set; } = string.Empty;
}

internal sealed class TphVideo : TphItem
{
    public int DurationSeconds { get; set; }
}

internal abstract class TptItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class TptArticle : TptItem
{
    public string Body { get; set; } = string.Empty;
}

internal sealed class TptVideo : TptItem
{
    public int DurationSeconds { get; set; }
}

internal abstract class TpcItem
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;
}

internal sealed class TpcArticle : TpcItem
{
    public string Body { get; set; } = string.Empty;
}

internal sealed class TpcVideo : TpcItem
{
    public int DurationSeconds { get; set; }
}
