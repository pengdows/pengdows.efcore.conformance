using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class InheritanceTests
{
    [Fact]
    [BehavioralCoverage("EF10-INHERITANCE-TPH-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.HasDiscriminator", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync", "System.Linq.Queryable.OfType")]
    public async Task Tph_PersistsAndMaterializesDerivedTypesAsynchronously()
    {
        var articleId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.TphItems.AddRange(
                new TphArticle { Id = articleId, Name = "Article", Body = "Article body" },
                new TphVideo { Id = videoId, Name = "Video", DurationSeconds = 120 });
            Assert.Equal(2, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var items = await read.TphItems.Where(item => item.Id == articleId || item.Id == videoId)
            .OrderBy(item => item.Name)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Collection(items,
            item => Assert.Equal("Article body", Assert.IsType<TphArticle>(item).Body),
            item => Assert.Equal(120, Assert.IsType<TphVideo>(item).DurationSeconds));
        Assert.Equal("Article body", await read.TphItems.OfType<TphArticle>()
            .Where(item => item.Id == articleId)
            .Select(item => item.Body)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-INHERITANCE-TPH-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder.HasDiscriminator", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.ToList", "System.Linq.Queryable.OfType")]
    public async Task Tph_PersistsAndMaterializesDerivedTypesSynchronously()
    {
        var articleId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.TphItems.AddRange(
                new TphArticle { Id = articleId, Name = "Article", Body = "Article body" },
                new TphVideo { Id = videoId, Name = "Video", DurationSeconds = 120 });
            Assert.Equal(2, write.SaveChanges());
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var items = read.TphItems.Where(item => item.Id == articleId || item.Id == videoId)
            .OrderBy(item => item.Name)
            .ToList();
        Assert.Collection(items,
            item => Assert.Equal("Article body", Assert.IsType<TphArticle>(item).Body),
            item => Assert.Equal(120, Assert.IsType<TphVideo>(item).DurationSeconds));
        Assert.Equal("Article body", read.TphItems.OfType<TphArticle>()
            .Where(item => item.Id == articleId)
            .Select(item => item.Body)
            .Single());
    }

    [Fact]
    [BehavioralCoverage("EF10-INHERITANCE-TPT-ASYNC", "Microsoft.EntityFrameworkCore.RelationalEntityTypeBuilderExtensions.UseTptMappingStrategy", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync", "System.Linq.Queryable.OfType")]
    public async Task Tpt_PersistsAndMaterializesDerivedTypesAsynchronously()
    {
        var articleId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        int written;
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.TptItems.AddRange(
                new TptArticle { Id = articleId, Name = "Article", Body = "Article body" },
                new TptVideo { Id = videoId, Name = "Video", DurationSeconds = 120 });
            written = await write.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var items = await read.TptItems.Where(item => item.Id == articleId || item.Id == videoId)
            .OrderBy(item => item.Name)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Collection(items,
            item => Assert.Equal("Article body", Assert.IsType<TptArticle>(item).Body),
            item => Assert.Equal(120, Assert.IsType<TptVideo>(item).DurationSeconds));
        Assert.Equal("Article body", await read.TptItems.OfType<TptArticle>()
            .Where(item => item.Id == articleId)
            .Select(item => item.Body)
            .SingleAsync(TestContext.Current.CancellationToken));
        // SaveChanges counts tracked state entries, not the multiple TPT rows per entry.
        Assert.Equal(2, written);
    }

    [Fact]
    [BehavioralCoverage("EF10-INHERITANCE-TPT-SYNC", "Microsoft.EntityFrameworkCore.RelationalEntityTypeBuilderExtensions.UseTptMappingStrategy", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.ToList", "System.Linq.Queryable.OfType")]
    public async Task Tpt_PersistsAndMaterializesDerivedTypesSynchronously()
    {
        var articleId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        int written;
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.TptItems.AddRange(
                new TptArticle { Id = articleId, Name = "Article", Body = "Article body" },
                new TptVideo { Id = videoId, Name = "Video", DurationSeconds = 120 });
            written = write.SaveChanges();
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var items = read.TptItems.Where(item => item.Id == articleId || item.Id == videoId)
            .OrderBy(item => item.Name)
            .ToList();
        Assert.Collection(items,
            item => Assert.Equal("Article body", Assert.IsType<TptArticle>(item).Body),
            item => Assert.Equal(120, Assert.IsType<TptVideo>(item).DurationSeconds));
        Assert.Equal("Article body", read.TptItems.OfType<TptArticle>()
            .Where(item => item.Id == articleId)
            .Select(item => item.Body)
            .Single());
        // SaveChanges counts tracked state entries, not the multiple TPT rows per entry.
        Assert.Equal(2, written);
    }

    [Fact]
    [BehavioralCoverage("EF10-INHERITANCE-TPC-ASYNC", "Microsoft.EntityFrameworkCore.RelationalEntityTypeBuilderExtensions.UseTpcMappingStrategy", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync", "System.Linq.Queryable.OfType")]
    public async Task Tpc_PersistsAndMaterializesDerivedTypesAsynchronously()
    {
        var articleId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.TpcItems.AddRange(
                new TpcArticle { Id = articleId, Name = "Article", Body = "Article body" },
                new TpcVideo { Id = videoId, Name = "Video", DurationSeconds = 120 });
            Assert.Equal(2, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var items = await read.TpcItems.Where(item => item.Id == articleId || item.Id == videoId)
            .OrderBy(item => item.Name)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Collection(items,
            item => Assert.Equal("Article body", Assert.IsType<TpcArticle>(item).Body),
            item => Assert.Equal(120, Assert.IsType<TpcVideo>(item).DurationSeconds));
        Assert.Equal("Article body", await read.TpcItems.OfType<TpcArticle>()
            .Where(item => item.Id == articleId)
            .Select(item => item.Body)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-INHERITANCE-TPC-SYNC", "Microsoft.EntityFrameworkCore.RelationalEntityTypeBuilderExtensions.UseTpcMappingStrategy", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.ToList", "System.Linq.Queryable.OfType")]
    public async Task Tpc_PersistsAndMaterializesDerivedTypesSynchronously()
    {
        var articleId = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.TpcItems.AddRange(
                new TpcArticle { Id = articleId, Name = "Article", Body = "Article body" },
                new TpcVideo { Id = videoId, Name = "Video", DurationSeconds = 120 });
            Assert.Equal(2, write.SaveChanges());
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var items = read.TpcItems.Where(item => item.Id == articleId || item.Id == videoId)
            .OrderBy(item => item.Name)
            .ToList();
        Assert.Collection(items,
            item => Assert.Equal("Article body", Assert.IsType<TpcArticle>(item).Body),
            item => Assert.Equal(120, Assert.IsType<TpcVideo>(item).DurationSeconds));
        Assert.Equal("Article body", read.TpcItems.OfType<TpcArticle>()
            .Where(item => item.Id == articleId)
            .Select(item => item.Body)
            .Single());
    }
}
