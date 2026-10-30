using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class DesignTimeMigrationCodeGenerationTests
{
    [Fact]
    [BehavioralCoverage(
        "EF10-DESIGN-SCAFFOLDING-MODEL-HELPERS",
        "Microsoft.EntityFrameworkCore.ScaffoldingModelExtensions.GetDbSetName",
        "Microsoft.EntityFrameworkCore.ScaffoldingModelExtensions.IsHandledByConvention",
        "Microsoft.EntityFrameworkCore.ScaffoldingModelExtensions.IsHandledByDataAnnotations",
        "Microsoft.EntityFrameworkCore.ScaffoldingModelExtensions.IsSimpleManyToManyJoinEntityType",
        "Microsoft.EntityFrameworkCore.ScaffoldingModelExtensions.IsLeftNavigation",
        "Microsoft.EntityFrameworkCore.ScaffoldingModelExtensions.GetDataAnnotations",
        "Microsoft.EntityFrameworkCore.ScaffoldingModelExtensions.GetFluentApiCalls")]
    public void ScaffoldingModelExtensions_DescribeConfiguredModel()
    {
        var options = new DbContextOptionsBuilder();
        ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString);
        using var context = new ScaffoldingDesignContext(options.Options);
        var services = new ServiceCollection();
        services.AddEntityFrameworkDesignTimeServices();
        services.AddDbContextDesignTimeServices(context);
        var relationalTypeMappingSource = context.GetService<IRelationalTypeMappingSource>();
        services.AddSingleton<IRelationalTypeMappingSource>(relationalTypeMappingSource);
        services.AddSingleton<ITypeMappingSource>(relationalTypeMappingSource);
        new EntityFrameworkRelationalDesignServicesBuilder(services).TryAddCoreServices();
        using var serviceProvider = services.BuildServiceProvider();

        var annotationCodeGenerator = serviceProvider.GetRequiredService<IAnnotationCodeGenerator>();
        var model = context.GetService<IDesignTimeModel>().Model;
        var post = model.FindEntityType(typeof(ScaffoldingPost))!;
        var tag = model.FindEntityType(typeof(ScaffoldingTag))!;
        var skipNavigation = post.GetSkipNavigations().Single();
        var join = skipNavigation.JoinEntityType;
        var primaryKey = post.FindPrimaryKey()!;
        var title = post.FindProperty(nameof(ScaffoldingPost.Title))!;
        var index = post.GetIndexes().Single(candidate => candidate.Properties.Contains(title));
        var navigation = post.GetNavigations().Single(candidate => candidate.Name == nameof(ScaffoldingPost.Blog));

        Assert.Equal(nameof(ScaffoldingDesignContext.Posts), post.GetDbSetName());
        Assert.True(primaryKey.IsHandledByConvention());
        Assert.True(index.IsHandledByDataAnnotations(annotationCodeGenerator));
        Assert.True(join.IsSimpleManyToManyJoinEntityType());
        Assert.NotEqual(skipNavigation.IsLeftNavigation(), skipNavigation.Inverse.IsLeftNavigation());
        Assert.NotEmpty(post.GetDataAnnotations(annotationCodeGenerator));
        Assert.NotEmpty(title.GetDataAnnotations(annotationCodeGenerator));
        Assert.NotNull(navigation.GetDataAnnotations(annotationCodeGenerator));
        Assert.NotNull(skipNavigation.GetDataAnnotations(annotationCodeGenerator));
        AssertFluentApiFragment(model.GetFluentApiCalls(annotationCodeGenerator));
        AssertFluentApiFragment(post.GetFluentApiCalls(annotationCodeGenerator));
        AssertFluentApiFragment(primaryKey.GetFluentApiCalls(annotationCodeGenerator));
        Assert.Equal("IsUnique", index.GetFluentApiCalls(annotationCodeGenerator)!.Method);
        var titleFluentApi = title.GetFluentApiCalls(annotationCodeGenerator)!;
        Assert.Equal("IsRequired", titleFluentApi.Method);
        Assert.True(titleFluentApi.IsHandledByDataAnnotations);
        AssertFluentApiFragment(navigation.ForeignKey.GetFluentApiCalls(annotationCodeGenerator));
        AssertFluentApiFragment(tag.GetFluentApiCalls(annotationCodeGenerator));
    }

    private static void AssertFluentApiFragment(FluentApiCodeFragment? fragment)
    {
        if (fragment is not null)
        {
            Assert.False(string.IsNullOrWhiteSpace(fragment.Method));
            Assert.True(fragment.Arguments.Count > 0 || fragment.ChainedCall is not null);
        }
    }

    [Fact]
    [BehavioralCoverage(
        "EF10-DESIGN-MIGRATION-CODE-GENERATION",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsCodeGeneratorSelector.Select",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsCodeGenerator.GenerateMigration",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsCodeGenerator.GenerateMetadata",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsCodeGenerator.GenerateSnapshot",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsCodeGenerator.FileExtension",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsScaffolder.ScaffoldMigration",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsScaffolder.Save",
        "Microsoft.EntityFrameworkCore.Migrations.Design.MigrationFiles..ctor",
        "Microsoft.EntityFrameworkCore.Migrations.Design.MigrationFiles.Migration",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.FileExtension",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.PreviousMigrationId",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.MigrationCode",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.MigrationId",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.MetadataCode",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.MigrationSubNamespace",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.SnapshotCode",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.SnapshotName",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.SnapshotSubnamespace",
        "Microsoft.EntityFrameworkCore.Migrations.Design.CSharpMigrationsGenerator.Language",
        "Microsoft.EntityFrameworkCore.Migrations.Design.CSharpMigrationsGenerator.FileExtension",
        "Microsoft.EntityFrameworkCore.Migrations.Design.CSharpMigrationsGenerator.GenerateMigration",
        "Microsoft.EntityFrameworkCore.Migrations.Design.CSharpMigrationsGenerator.GenerateMetadata",
        "Microsoft.EntityFrameworkCore.Migrations.Design.CSharpMigrationsGenerator.GenerateSnapshot",
        "Microsoft.EntityFrameworkCore.Migrations.Design.MigrationsCodeGenerator.GenerateMigration",
        "Microsoft.EntityFrameworkCore.Migrations.Design.MigrationsCodeGenerator.GenerateMetadata",
        "Microsoft.EntityFrameworkCore.Migrations.Design.MigrationsCodeGenerator.GenerateSnapshot",
        "Microsoft.EntityFrameworkCore.Migrations.Design.MigrationsCodeGenerator.FileExtension",
        "Microsoft.EntityFrameworkCore.Migrations.Design.MigrationsCodeGenerator.Language",
        "Microsoft.EntityFrameworkCore.Migrations.Design.MigrationsScaffolder.ScaffoldMigration",
        "Microsoft.EntityFrameworkCore.Migrations.Design.MigrationsScaffolder.Save",
        "Microsoft.EntityFrameworkCore.Migrations.Design.CSharpMigrationOperationGenerator.Generate",
        "Microsoft.EntityFrameworkCore.Migrations.Design.CSharpSnapshotGenerator.Generate",
        "Microsoft.EntityFrameworkCore.Migrations.Design.MigrationFiles.MigrationFile",
        "Microsoft.EntityFrameworkCore.Migrations.Design.MigrationFiles.MetadataFile",
        "Microsoft.EntityFrameworkCore.Migrations.Design.MigrationFiles.SnapshotFile",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ICSharpMigrationOperationGenerator.Generate",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ICSharpSnapshotGenerator.Generate")]
    public void CSharpMigrationCodeGenerator_EmitsDeclaredOperation()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkDesignTimeServices();
        using var context = ProviderTestRuntime.CreateContext();
        services.AddDbContextDesignTimeServices(context);
        var relationalTypeMappingSource = context.GetService<IRelationalTypeMappingSource>();
        services.AddSingleton<IRelationalTypeMappingSource>(relationalTypeMappingSource);
        services.AddSingleton<ITypeMappingSource>(relationalTypeMappingSource);
        new EntityFrameworkRelationalDesignServicesBuilder(services).TryAddCoreServices();
        using var serviceProvider = services.BuildServiceProvider();

        var generator = serviceProvider
            .GetRequiredService<IMigrationsCodeGeneratorSelector>()
            .Select("C#");
        var csharpGenerator = Assert.IsType<CSharpMigrationsGenerator>(generator);
        var baseGenerator = Assert.IsAssignableFrom<MigrationsCodeGenerator>(csharpGenerator);
        var designModel = context.GetService<IDesignTimeModel>().Model;
        var operation = new AddColumnOperation
        {
            Schema = "public",
            Table = "people",
            Name = "age",
            ClrType = typeof(int),
            ColumnType = "integer",
            IsNullable = false
        };

        var generated = generator.GenerateMigration(
            "Example.Migrations",
            "AddPersonAge",
            [operation],
            Array.Empty<MigrationOperation>());
        Assert.Equal(generated, baseGenerator.GenerateMigration(
            "Example.Migrations",
            "AddPersonAge",
            [operation],
            Array.Empty<MigrationOperation>()));
        var metadata = generator.GenerateMetadata(
            "Example.Migrations",
            typeof(ConformanceDbContext),
            "AddPersonAge",
            "202610300001_AddPersonAge",
            designModel);
        Assert.Equal(metadata, baseGenerator.GenerateMetadata(
            "Example.Migrations",
            typeof(ConformanceDbContext),
            "AddPersonAge",
            "202610300001_AddPersonAge",
            designModel));
        var snapshot = generator.GenerateSnapshot(
            "Example.Migrations",
            typeof(ConformanceDbContext),
            "ConformanceContextModelSnapshot",
            designModel);
        Assert.Equal(snapshot, baseGenerator.GenerateSnapshot(
            "Example.Migrations",
            typeof(ConformanceDbContext),
            "ConformanceContextModelSnapshot",
            designModel));
        var scaffoldedMigration = serviceProvider.GetRequiredService<IMigrationsScaffolder>()
            .ScaffoldMigration("ConformanceInitial", "Example.Migrations", language: "C#", dryRun: true);
        var scaffolder = Assert.IsType<MigrationsScaffolder>(serviceProvider.GetRequiredService<IMigrationsScaffolder>());
        var overloadScaffold = scaffolder.ScaffoldMigration("ConformanceOverload", "Example.Migrations", "ExplicitSubnamespace");
        var projectDirectory = Path.Combine(Path.GetTempPath(), $"ef10-design-{Guid.NewGuid():N}");
        var savedFiles = scaffolder.Save(projectDirectory, scaffoldedMigration, "Migrations", dryRun: true);
        var operationBuilder = new IndentedStringBuilder();
        var operationGenerator = Assert.IsType<CSharpMigrationOperationGenerator>(
            serviceProvider.GetRequiredService<ICSharpMigrationOperationGenerator>());
        operationGenerator.Generate("migrationBuilder", [operation], operationBuilder);
        var snapshotBuilder = new IndentedStringBuilder();
        var snapshotGenerator = Assert.IsType<CSharpSnapshotGenerator>(
            serviceProvider.GetRequiredService<ICSharpSnapshotGenerator>());
        snapshotGenerator.Generate("modelBuilder", designModel, snapshotBuilder);

        Assert.Equal(".cs", generator.FileExtension);
        Assert.Equal("C#", csharpGenerator.Language);
        Assert.Equal(".cs", csharpGenerator.FileExtension);
        Assert.Equal("C#", baseGenerator.Language);
        Assert.Equal(".cs", baseGenerator.FileExtension);
        Assert.Contains("AddColumn<int>", generated, StringComparison.Ordinal);
        Assert.Contains("name: \"age\"", generated, StringComparison.Ordinal);
        Assert.Contains("table: \"people\"", generated, StringComparison.Ordinal);
        Assert.Contains("type: \"integer\"", generated, StringComparison.Ordinal);
        Assert.Contains("AddPersonAge", metadata, StringComparison.Ordinal);
        Assert.Contains("ConformanceContextModelSnapshot", snapshot, StringComparison.Ordinal);
        Assert.Contains("ef10conformancepeople", snapshot, StringComparison.Ordinal);
        Assert.Contains("CreateTable", scaffoldedMigration.MigrationCode, StringComparison.Ordinal);
        Assert.Contains("ef10conformancepeople", scaffoldedMigration.MigrationCode, StringComparison.Ordinal);
        Assert.Equal(".cs", scaffoldedMigration.FileExtension);
        Assert.Contains("ConformanceInitial", scaffoldedMigration.MigrationId, StringComparison.Ordinal);
        Assert.Contains("ConformanceInitial", scaffoldedMigration.MetadataCode, StringComparison.Ordinal);
        Assert.Contains("ModelSnapshot", scaffoldedMigration.SnapshotCode, StringComparison.Ordinal);
        Assert.Contains("CreateTable", overloadScaffold.MigrationCode, StringComparison.Ordinal);
        Assert.Equal("ExplicitSubnamespace", overloadScaffold.MigrationSubNamespace);
        Assert.Equal(context.Database.GetMigrations().LastOrDefault(), scaffoldedMigration.PreviousMigrationId);
        Assert.Contains("ConformanceDb", scaffoldedMigration.MigrationSubNamespace, StringComparison.Ordinal);
        Assert.Equal("ConformanceDbContextModelSnapshot", scaffoldedMigration.SnapshotName);
        Assert.Contains("ConformanceDb", scaffoldedMigration.SnapshotSubnamespace, StringComparison.Ordinal);
        Assert.EndsWith(".cs", savedFiles.MigrationFile, StringComparison.Ordinal);
        Assert.EndsWith(".Designer.cs", savedFiles.MetadataFile, StringComparison.Ordinal);
        Assert.EndsWith("ModelSnapshot.cs", savedFiles.SnapshotFile, StringComparison.Ordinal);
        Assert.Same(scaffoldedMigration, savedFiles.Migration);
        Assert.False(Directory.Exists(projectDirectory));
        Assert.Contains("AddColumn<int>", operationBuilder.ToString(), StringComparison.Ordinal);
        Assert.Contains("ef10conformancepeople", snapshotBuilder.ToString(), StringComparison.Ordinal);
    }
}

internal sealed class ScaffoldingDesignContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<ScaffoldingPost> Posts => Set<ScaffoldingPost>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScaffoldingBlog>().HasMany(blog => blog.Posts).WithOne(post => post.Blog);
        modelBuilder.Entity<ScaffoldingPost>()
            .HasIndex(post => post.Title)
            .IsUnique();
        modelBuilder.Entity<ScaffoldingPost>().HasMany(post => post.Tags).WithMany(tag => tag.Posts);
    }
}

internal sealed class ScaffoldingBlog
{
    public int Id { get; set; }

    public ICollection<ScaffoldingPost> Posts { get; set; } = new List<ScaffoldingPost>();
}

internal sealed class ScaffoldingPost
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public int BlogId { get; set; }

    public ScaffoldingBlog Blog { get; set; } = null!;

    public ICollection<ScaffoldingTag> Tags { get; set; } = new List<ScaffoldingTag>();
}

internal sealed class ScaffoldingTag
{
    public int Id { get; set; }

    public ICollection<ScaffoldingPost> Posts { get; set; } = new List<ScaffoldingPost>();
}
