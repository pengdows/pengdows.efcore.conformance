using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace pengdows.efcore.conformance.EF10.Relational;

[DbContext(typeof(ConformanceDbContext))]
[Migration("20261029000101_EF10Probe")]
public sealed class Ef10ProbeMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable(
        name: "ef10conformance_migrationprobe",
        columns: table => new
        {
            id = table.Column<Guid>(type: null, nullable: false),
            value = table.Column<int>(type: null, nullable: false)
        },
        constraints: table => table.PrimaryKey("PK_ef10conformance_migrationprobe", row => row.id));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "ef10conformance_migrationprobe");

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationProbeRow>(entity =>
        {
            entity.ToTable("ef10conformance_migrationprobe");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Value).HasColumnName("value");
        });
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
    }
}

public sealed class MigrationProbeRow
{
    public Guid Id { get; set; }

    public int Value { get; set; }

    public string? Description { get; set; }
}

[DbContext(typeof(ConformanceDbContext))]
[Migration("20261029000102_EF10MigrationOperations")]
public sealed class Ef10MigrationOperationsMigration : Migration
{
    private static readonly Guid SeedRowId = Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520");

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "label",
            table: "ef10conformance_migrationprobe",
            type: null,
            nullable: true);
        migrationBuilder.CreateIndex(
            name: "IX_ef10conformance_migrationprobe_value",
            table: "ef10conformance_migrationprobe",
            column: "value");
        migrationBuilder.InsertData(
            table: "ef10conformance_migrationprobe",
            columns: ["id", "value"],
            values: [SeedRowId, 123]);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "ef10conformance_migrationprobe",
            keyColumn: "id",
            keyValue: SeedRowId);
        migrationBuilder.DropIndex(
            name: "IX_ef10conformance_migrationprobe_value",
            table: "ef10conformance_migrationprobe");
        migrationBuilder.DropColumn(
            name: "label",
            table: "ef10conformance_migrationprobe");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationProbeRow>(entity =>
        {
            entity.ToTable("ef10conformance_migrationprobe");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Value).HasColumnName("value");
            entity.Property(row => row.Description).HasColumnName("label");
        });
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
    }
}

[DbContext(typeof(ConformanceDbContext))]
[Migration("20261029000103_EF10MigrationChanges")]
public sealed class Ef10MigrationChangesMigration : Migration
{
    private static readonly Guid SeedRowId = Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520");

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameColumn(
            name: "label",
            table: "ef10conformance_migrationprobe",
            newName: "description");
        migrationBuilder.DropIndex(
            name: "IX_ef10conformance_migrationprobe_value",
            table: "ef10conformance_migrationprobe");
        migrationBuilder.UpdateData(
            table: "ef10conformance_migrationprobe",
            keyColumn: "id",
            keyValue: SeedRowId,
            column: "value",
            value: 314159);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.UpdateData(
            table: "ef10conformance_migrationprobe",
            keyColumn: "id",
            keyValue: SeedRowId,
            column: "value",
            value: 123);
        migrationBuilder.CreateIndex(
            name: "IX_ef10conformance_migrationprobe_value",
            table: "ef10conformance_migrationprobe",
            column: "value");
        migrationBuilder.RenameColumn(
            name: "description",
            table: "ef10conformance_migrationprobe",
            newName: "label");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationProbeRow>(entity =>
        {
            entity.ToTable("ef10conformance_migrationprobe");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Value).HasColumnName("value");
            entity.Property(row => row.Description).HasColumnName("description");
        });
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
    }
}

[DbContext(typeof(ConformanceDbContext))]
[Migration("20261029000104_EF10MigrationRenameTable")]
public sealed class Ef10MigrationRenameTableMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(
            name: "ef10conformance_migrationprobe",
            newName: "ef10conformance_migrationprobe_renamed");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameTable(
            name: "ef10conformance_migrationprobe_renamed",
            newName: "ef10conformance_migrationprobe");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationProbeRow>(entity =>
        {
            entity.ToTable("ef10conformance_migrationprobe_renamed");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Value).HasColumnName("value");
            entity.Property(row => row.Description).HasColumnName("description");
        });
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
    }
}
