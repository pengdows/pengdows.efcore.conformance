using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("migration-set-default")]
public sealed class SetDefaultMigrationTests
{
    [Fact]
    [BehavioralCoverage(
        "EF10-MIGRATIONS-SET-DEFAULT-ASYNC",
        "Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetDefault",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateTable",
        "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder`1.ForeignKey",
        "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync",
        "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync",
        "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task SetDefaultForeignKey_UsesTheConfiguredDefaultValueAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("migration-set-default");
        try
        {
            await using (var setup = CreateContext())
            {
                await setup.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                await setup.Database.MigrateAsync(TestContext.Current.CancellationToken);
                var parentToDelete = new SetDefaultParent { Id = 1 };
                setup.Parents.AddRange(new SetDefaultParent { Id = 0 }, parentToDelete);
                Assert.Equal(2, await setup.SaveChangesAsync(TestContext.Current.CancellationToken));
                setup.Children.Add(new SetDefaultChild { Id = 10, ParentId = 1 });
                Assert.Equal(1, await setup.SaveChangesAsync(TestContext.Current.CancellationToken));
                setup.Parents.Remove(parentToDelete);
                Assert.Equal(1, await setup.SaveChangesAsync(TestContext.Current.CancellationToken));
            }

            await using var verify = CreateContext();
            var child = await verify.Children.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, child.ParentId);
        }
        finally
        {
            await using var cleanup = CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    [Fact]
    [BehavioralCoverage(
        "EF10-MIGRATIONS-SET-DEFAULT-SYNC",
        "Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetDefault",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateTable",
        "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder`1.ForeignKey",
        "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate",
        "Microsoft.EntityFrameworkCore.DbContext.SaveChanges",
        "System.Linq.Queryable.Single")]
    public async Task SetDefaultForeignKey_UsesTheConfiguredDefaultValueSynchronously()
    {
        ProviderTestRuntime.RequireCapability("migration-set-default");
        try
        {
            await using (var setup = CreateContext())
            {
                await setup.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                setup.Database.Migrate();
                var parentToDelete = new SetDefaultParent { Id = 1 };
                setup.Parents.AddRange(new SetDefaultParent { Id = 0 }, parentToDelete);
                Assert.Equal(2, setup.SaveChanges());
                setup.Children.Add(new SetDefaultChild { Id = 10, ParentId = 1 });
                Assert.Equal(1, setup.SaveChanges());
                setup.Parents.Remove(parentToDelete);
                Assert.Equal(1, setup.SaveChanges());
            }

            await using var verify = CreateContext();
            var child = verify.Children.AsNoTracking().Single();
            Assert.Equal(0, child.ParentId);
        }
        finally
        {
            await using var cleanup = CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    private static SetDefaultMigrationContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<SetDefaultMigrationContext>();
        ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString);
        return new SetDefaultMigrationContext(options.Options);
    }
}

internal sealed class SetDefaultMigrationContext(DbContextOptions<SetDefaultMigrationContext> options) : DbContext(options)
{
    public DbSet<SetDefaultParent> Parents => Set<SetDefaultParent>();

    public DbSet<SetDefaultChild> Children => Set<SetDefaultChild>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SetDefaultParent>(entity =>
        {
            entity.ToTable("ef10conformance_setdefault_parent");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        });
        modelBuilder.Entity<SetDefaultChild>(entity =>
        {
            entity.ToTable("ef10conformance_setdefault_child");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(row => row.ParentId).HasColumnName("parentid");
        });
    }
}

internal sealed class SetDefaultParent
{
    public int Id { get; set; }
}

internal sealed class SetDefaultChild
{
    public int Id { get; set; }

    public int ParentId { get; set; }
}

[DbContext(typeof(SetDefaultMigrationContext))]
[Migration("20261030000101_EF10SetDefaultParent")]
internal sealed class SetDefaultParentMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable(
        name: "ef10conformance_setdefault_parent",
        columns: table => new { id = table.Column<int>(type: null, nullable: false) },
        constraints: table => table.PrimaryKey("PK_ef10conformance_setdefault_parent", row => row.id));

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("ef10conformance_setdefault_parent");

    protected override void BuildTargetModel(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<SetDefaultParent>().ToTable("ef10conformance_setdefault_parent").HasKey(row => row.Id);
}

[DbContext(typeof(SetDefaultMigrationContext))]
[Migration("20261030000102_EF10SetDefaultChild")]
internal sealed class SetDefaultChildMigration : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.CreateTable(
        name: "ef10conformance_setdefault_child",
        columns: table => new
        {
            id = table.Column<int>(type: null, nullable: false),
            parentid = table.Column<int>(type: null, nullable: false, defaultValue: 0)
        },
        constraints: table =>
        {
            table.PrimaryKey("PK_ef10conformance_setdefault_child", row => row.id);
            table.ForeignKey(
                name: "FK_ef10conformance_setdefault_child_parent",
                column: row => row.parentid,
                principalTable: "ef10conformance_setdefault_parent",
                principalColumn: "id",
                onDelete: ReferentialAction.SetDefault);
        });

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable("ef10conformance_setdefault_child");

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SetDefaultParent>().ToTable("ef10conformance_setdefault_parent").HasKey(row => row.Id);
        modelBuilder.Entity<SetDefaultChild>(entity =>
        {
            entity.ToTable("ef10conformance_setdefault_child");
            entity.HasKey(row => row.Id);
            entity.Property(row => row.ParentId).HasColumnName("parentid").HasDefaultValue(0);
        });
    }
}
