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
        migrationBuilder.CreateTable(
            name: "ef10conformance_migrationchild",
            columns: table => new
            {
                id = table.Column<Guid>(type: null, nullable: false),
                probeid = table.Column<Guid>(type: null, nullable: false),
                code = table.Column<string>(type: null, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ef10conformance_migrationchild", row => row.id);
                table.ForeignKey(
                    name: "FK_ef10conformance_migrationchild_probe",
                    column: row => row.probeid,
                    principalTable: "ef10conformance_migrationprobe",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.UniqueConstraint("AK_ef10conformance_migrationchild_code", row => row.code);
                table.CheckConstraint("CK_ef10conformance_migrationchild_code_nonempty", "code <> ''");
            });
        migrationBuilder.CreateIndex(
            name: "IX_ef10conformance_migrationchild_probeid",
            table: "ef10conformance_migrationchild",
            column: "probeid");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            table: "ef10conformance_migrationprobe",
            keyColumn: "id",
            keyValue: SeedRowId);
        migrationBuilder.DropTable(name: "ef10conformance_migrationchild");
        migrationBuilder.DropIndex(
            name: "IX_ef10conformance_migrationprobe_value",
            table: "ef10conformance_migrationprobe");
        migrationBuilder.DropColumn(
            name: "label",
            table: "ef10conformance_migrationprobe");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationChildRow>(entity =>
        {
            entity.ToTable("ef10conformance_migrationchild");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.ProbeId).HasColumnName("probeid");
            entity.Property(row => row.Code).HasColumnName("code");
            entity.HasOne<MigrationProbeRow>().WithMany().HasForeignKey(row => row.ProbeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasAlternateKey(row => row.Code);
        });
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

public sealed class MigrationChildRow
{
    public Guid Id { get; set; }

    public Guid ProbeId { get; set; }

    public string Code { get; set; } = string.Empty;
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

[DbContext(typeof(ConformanceDbContext))]
[Migration("20261029000105_EF10MigrationSqlOperation")]
public sealed class Ef10MigrationSqlOperationMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE TABLE ef10conformance_migrationsql (id INTEGER NOT NULL PRIMARY KEY, value INTEGER NOT NULL)");
        migrationBuilder.Sql("INSERT INTO ef10conformance_migrationsql (id, value) VALUES (1, 4242)");
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql("DROP TABLE ef10conformance_migrationsql");

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationSqlOperationRow>(entity =>
        {
            entity.ToTable("ef10conformance_migrationsql");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Value).HasColumnName("value");
        });
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
    }
}

[DbContext(typeof(ConformanceDbContext))]
[Migration("20261029000106_EF10MigrationIndexRename")]
public sealed class Ef10MigrationIndexRenameMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_ef10conformance_migrationprobe_value",
            table: "ef10conformance_migrationprobe_renamed",
            column: "value");
        migrationBuilder.RenameIndex(
            name: "IX_ef10conformance_migrationprobe_value",
            table: "ef10conformance_migrationprobe_renamed",
            newName: "IX_ef10conformance_migrationprobe_value_renamed");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.RenameIndex(
            name: "IX_ef10conformance_migrationprobe_value_renamed",
            table: "ef10conformance_migrationprobe_renamed",
            newName: "IX_ef10conformance_migrationprobe_value");
        migrationBuilder.DropIndex(
            name: "IX_ef10conformance_migrationprobe_value",
            table: "ef10conformance_migrationprobe_renamed");
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
            entity.HasIndex(row => row.Value).HasDatabaseName("IX_ef10conformance_migrationprobe_value_renamed");
        });
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
    }
}

[DbContext(typeof(ConformanceDbContext))]
[Migration("20261029000107_EF10MigrationCheckConstraint")]
public sealed class Ef10MigrationCheckConstraintMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddCheckConstraint(
            name: "CK_ef10conformance_migrationprobe_value_positive",
            table: "ef10conformance_migrationprobe_renamed",
            sql: "value > 0");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_ef10conformance_migrationprobe_value_positive",
            table: "ef10conformance_migrationprobe_renamed");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationProbeRow>(entity =>
        {
            entity.ToTable("ef10conformance_migrationprobe_renamed", table =>
                table.HasCheckConstraint("CK_ef10conformance_migrationprobe_value_positive", "value > 0"));
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Value).HasColumnName("value");
            entity.Property(row => row.Description).HasColumnName("description");
        });
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
    }
}

[DbContext(typeof(ConformanceDbContext))]
[Migration("20261029000108_EF10MigrationAddConstraints")]
public sealed class Ef10MigrationAddConstraintsMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        if (string.IsNullOrWhiteSpace(ActiveProvider) || string.IsNullOrWhiteSpace(migrationBuilder.ActiveProvider))
        {
            throw new InvalidOperationException("EF Core must populate the active provider while applying a migration.");
        }

        migrationBuilder.CreateTable(
            name: "ef10conformance_migrationconstraints",
            columns: table => new
            {
                id = table.Column<int>(type: null, nullable: false),
                probeid = table.Column<Guid>(type: null, nullable: false),
                code = table.Column<string>(type: null, nullable: false)
            });
        migrationBuilder.AddPrimaryKey(
            name: "PK_ef10conformance_migrationconstraints",
            table: "ef10conformance_migrationconstraints",
            column: "id");
        migrationBuilder.AddUniqueConstraint(
            name: "AK_ef10conformance_migrationconstraints_code",
            table: "ef10conformance_migrationconstraints",
            column: "code");
        migrationBuilder.AddForeignKey(
            name: "FK_ef10conformance_migrationconstraints_probe",
            table: "ef10conformance_migrationconstraints",
            column: "probeid",
            principalTable: "ef10conformance_migrationprobe_renamed",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade);
        migrationBuilder.CreateCheckConstraint(
            name: "CK_ef10conformance_migrationconstraints_code_nonempty",
            table: "ef10conformance_migrationconstraints",
            sql: "code <> ''");
        migrationBuilder.AlterTable(
            name: "ef10conformance_migrationconstraints",
            comment: "EF10 migration constraints table");
        migrationBuilder.AlterColumn<long>(
            name: "value",
            table: "ef10conformance_migrationprobe_renamed",
            nullable: false,
            oldClrType: typeof(int),
            oldNullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "value",
            table: "ef10conformance_migrationprobe_renamed",
            nullable: false,
            oldClrType: typeof(long),
            oldNullable: false);
        migrationBuilder.AlterTable(
            name: "ef10conformance_migrationconstraints",
            oldComment: "EF10 migration constraints table");
        migrationBuilder.DropCheckConstraint(
            name: "CK_ef10conformance_migrationconstraints_code_nonempty",
            table: "ef10conformance_migrationconstraints");
        migrationBuilder.DropForeignKey(
            name: "FK_ef10conformance_migrationconstraints_probe",
            table: "ef10conformance_migrationconstraints");
        migrationBuilder.DropUniqueConstraint(
            name: "AK_ef10conformance_migrationconstraints_code",
            table: "ef10conformance_migrationconstraints");
        migrationBuilder.DropPrimaryKey(
            name: "PK_ef10conformance_migrationconstraints",
            table: "ef10conformance_migrationconstraints");
        migrationBuilder.DropTable(name: "ef10conformance_migrationconstraints");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MigrationConstraintRow>(entity =>
        {
            entity.ToTable("ef10conformance_migrationconstraints");
            entity.HasKey(row => row.Id);
            entity.HasAlternateKey(row => row.Code);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.ProbeId).HasColumnName("probeid");
            entity.Property(row => row.Code).HasColumnName("code");
            entity.HasOne<MigrationProbeLongRow>().WithMany().HasForeignKey(row => row.ProbeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<MigrationProbeLongRow>(entity =>
        {
            entity.ToTable("ef10conformance_migrationprobe_renamed");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id");
            entity.Property(row => row.Value).HasColumnName("value").HasColumnType("bigint");
            entity.Property(row => row.Description).HasColumnName("description");
        });
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
    }
}

public sealed class MigrationConstraintRow
{
    public int Id { get; set; }

    public Guid ProbeId { get; set; }

    public string Code { get; set; } = string.Empty;
}

public sealed class MigrationProbeLongRow
{
    public Guid Id { get; set; }

    public long Value { get; set; }

    public string? Description { get; set; }
}

public sealed class MigrationSqlOperationRow
{
    public int Id { get; set; }

    public int Value { get; set; }
}
