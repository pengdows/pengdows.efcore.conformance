using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace pengdows.efcore.conformance.EF10.Relational;

[DbContext(typeof(ConformanceDbContext))]
[Migration("202610290001_EF10Probe")]
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

    protected override void BuildTargetModel(ModelBuilder modelBuilder) =>
        modelBuilder.HasAnnotation("ProductVersion", "10.0.14");
}
