using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
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
        "Microsoft.EntityFrameworkCore.Migrations.Design.IMigrationsCodeGenerator.GenerateSnapshot")]
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

        Assert.Equal(".cs", generator.FileExtension);
        Assert.Contains("AddColumn<int>", generated, StringComparison.Ordinal);
        Assert.Contains("name: \"age\"", generated, StringComparison.Ordinal);
        Assert.Contains("table: \"people\"", generated, StringComparison.Ordinal);
        Assert.Contains("type: \"integer\"", generated, StringComparison.Ordinal);
        Assert.Contains("AddPersonAge", metadata, StringComparison.Ordinal);
        Assert.Contains("ConformanceContextModelSnapshot", snapshot, StringComparison.Ordinal);
        Assert.Contains("ef10conformancepeople", snapshot, StringComparison.Ordinal);
    }
}
