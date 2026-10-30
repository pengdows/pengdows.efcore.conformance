using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
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
        "EF10-DESIGN-MIGRATION-CODE-GENERATION",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsCodeGeneratorSelector.Select",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsCodeGenerator.GenerateMigration",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsCodeGenerator.GenerateMetadata",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsCodeGenerator.GenerateSnapshot",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsScaffolder.ScaffoldMigration",
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsScaffolder.Save",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.FileExtension",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.MigrationCode",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.MigrationId",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.MetadataCode",
        "Microsoft.EntityFrameworkCore.Migrations.Design.ScaffoldedMigration.SnapshotCode",
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
        var metadata = generator.GenerateMetadata(
            "Example.Migrations",
            typeof(ConformanceDbContext),
            "AddPersonAge",
            "202610300001_AddPersonAge",
            designModel);
        var snapshot = generator.GenerateSnapshot(
            "Example.Migrations",
            typeof(ConformanceDbContext),
            "ConformanceContextModelSnapshot",
            designModel);
        var scaffoldedMigration = serviceProvider.GetRequiredService<IMigrationsScaffolder>()
            .ScaffoldMigration("ConformanceInitial", "Example.Migrations", language: "C#", dryRun: true);
        var scaffolder = serviceProvider.GetRequiredService<IMigrationsScaffolder>();
        var projectDirectory = Path.Combine(Path.GetTempPath(), $"ef10-design-{Guid.NewGuid():N}");
        var savedFiles = scaffolder.Save(projectDirectory, scaffoldedMigration, "Migrations", dryRun: true);
        var operationBuilder = new IndentedStringBuilder();
        serviceProvider.GetRequiredService<ICSharpMigrationOperationGenerator>()
            .Generate("migrationBuilder", [operation], operationBuilder);
        var snapshotBuilder = new IndentedStringBuilder();
        serviceProvider.GetRequiredService<ICSharpSnapshotGenerator>()
            .Generate("modelBuilder", designModel, snapshotBuilder);

        Assert.Equal(".cs", generator.FileExtension);
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
        Assert.EndsWith(".cs", savedFiles.MigrationFile, StringComparison.Ordinal);
        Assert.EndsWith(".Designer.cs", savedFiles.MetadataFile, StringComparison.Ordinal);
        Assert.EndsWith("ModelSnapshot.cs", savedFiles.SnapshotFile, StringComparison.Ordinal);
        Assert.False(Directory.Exists(projectDirectory));
        Assert.Contains("AddColumn<int>", operationBuilder.ToString(), StringComparison.Ordinal);
        Assert.Contains("ef10conformancepeople", snapshotBuilder.ToString(), StringComparison.Ordinal);
    }
}
