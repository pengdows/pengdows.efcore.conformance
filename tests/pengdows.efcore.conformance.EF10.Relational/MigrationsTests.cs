using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("migrations")]
public sealed class MigrationsTests
{
    [Fact]
    [BehavioralCoverage("EF10-MIGRATIONS-APPLY-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetMigrations", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetAppliedMigrationsAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetPendingMigrationsAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.HasPendingModelChanges", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SqlQuery", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AddColumn", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateIndex", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.InsertData", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.RenameColumn", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropIndex", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.UpdateData", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DeleteData", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropColumn", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateTable", "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder`1", "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder`1.PrimaryKey", "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder`1.ForeignKey", "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder`1.UniqueConstraint", "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder`1.CheckConstraint", "Microsoft.EntityFrameworkCore.Migrations.ReferentialAction", "Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropTable", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.RenameTable", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.Sql", "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute", "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute..ctor", "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute.Id", "Microsoft.EntityFrameworkCore.Migrations.Migration.TargetModel", "Microsoft.EntityFrameworkCore.Migrations.Migration.UpOperations", "Microsoft.EntityFrameworkCore.Migrations.Migration.DownOperations")]
    public async Task MigrateAsync_AppliesMigrationAndPersistsValues()
    {
        ProviderTestRuntime.RequireCapability("migrations");
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        const int expected = 314159;

        try
        {
            await using (var migrate = ProviderTestRuntime.CreateContext())
            {
                await migrate.Database.EnsureDeletedAsync(cancellationToken);
                var migrationIds = new[] { "20261029000101_EF10Probe", "20261029000102_EF10MigrationOperations", "20261029000103_EF10MigrationChanges", "20261029000104_EF10MigrationRenameTable", "20261029000105_EF10MigrationSqlOperation", "20261029000106_EF10MigrationIndexRename", "20261029000107_EF10MigrationCheckConstraint", "20261029000108_EF10MigrationAddConstraints", "20261030000103_EF10AlterDatabaseCollation" };
                Assert.Equal(migrationIds, migrate.Database.GetMigrations());
                var portableMigrations = migrationIds.Take(5).ToArray();
                await migrate.Database.MigrateAsync(portableMigrations[^1], cancellationToken);
                Assert.True(migrate.Database.HasPendingModelChanges());
                Assert.Equal(portableMigrations, await migrate.Database.GetAppliedMigrationsAsync(cancellationToken));
                Assert.Equal(migrationIds.Skip(portableMigrations.Length), await migrate.Database.GetPendingMigrationsAsync(cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationprobe_renamed (id, value, description) VALUES ({id}, {expected}, {"async row"})",
                    cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationchild (id, probeid, code) VALUES ({Guid.NewGuid()}, {id}, {"child-async"})",
                    cancellationToken));
                Assert.Equal(4242, await migrate.Database.SqlQuery<int>(
                    $"SELECT value AS Value FROM ef10conformance_migrationsql WHERE id = {1}")
                    .SingleAsync(cancellationToken));
                await Assert.ThrowsAnyAsync<DbException>(() => migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationchild (id, probeid, code) VALUES ({Guid.NewGuid()}, {id}, {string.Empty})",
                    cancellationToken));
                await Assert.ThrowsAnyAsync<DbException>(() => migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationchild (id, probeid, code) VALUES ({Guid.NewGuid()}, {id}, {"child-async"})",
                    cancellationToken));
                await Assert.ThrowsAnyAsync<DbException>(() => migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationchild (id, probeid, code) VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {"orphan-async"})",
                    cancellationToken));
                Assert.Equal(1, await migrate.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS Value FROM ef10conformance_migrationchild child INNER JOIN ef10conformance_migrationprobe_renamed probe ON child.probeid = probe.id WHERE probe.id = {id}")
                    .SingleAsync(cancellationToken));
                await Assert.ThrowsAnyAsync<DbException>(() => migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM ef10conformance_migrationprobe_renamed WHERE id = {id}", cancellationToken));
            }

            await using (var verify = ProviderTestRuntime.CreateContext())
            {
                var actual = await verify.Database.SqlQuery<int>(
                        $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {id}")
                    .SingleAsync(cancellationToken);
                var description = await verify.Database.SqlQuery<string>(
                        $"SELECT description AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {id}")
                    .SingleAsync(cancellationToken);
                var seeded = await verify.Database.SqlQuery<int>(
                        $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520")}")
                    .SingleAsync(cancellationToken);
                Assert.Equal(expected, actual);
                Assert.Equal("async row", description);
                Assert.Equal(314159, seeded);
                Assert.Equal(1, await verify.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS Value FROM ef10conformance_migrationchild WHERE probeid = {id}")
                    .SingleAsync(cancellationToken));
            }

            await using (var rollback = ProviderTestRuntime.CreateContext())
            {
                await rollback.Database.MigrateAsync("20261029000101_EF10Probe", cancellationToken);
                Assert.Equal(new[] { "20261029000101_EF10Probe" }, await rollback.Database.GetAppliedMigrationsAsync(cancellationToken));
            }

            await using (var verifyRollback = ProviderTestRuntime.CreateContext())
            {
                Assert.Equal(0, await verifyRollback.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS Value FROM ef10conformance_migrationprobe WHERE id = {Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520")}")
                    .SingleAsync(cancellationToken));
                Assert.Equal(1, await verifyRollback.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS Value FROM ef10conformance_migrationprobe WHERE id = {id}")
                    .SingleAsync(cancellationToken));
            }

            await using (var reapply = ProviderTestRuntime.CreateContext())
            {
                await reapply.Database.MigrateAsync("20261029000105_EF10MigrationSqlOperation", cancellationToken);
            }

            await using var verifyReapply = ProviderTestRuntime.CreateContext();
            Assert.Equal(314159, await verifyReapply.Database.SqlQuery<int>(
                    $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520")}")
                .SingleAsync(cancellationToken));
            Assert.Equal(4242, await verifyReapply.Database.SqlQuery<int>(
                    $"SELECT value AS Value FROM ef10conformance_migrationsql WHERE id = {1}")
                .SingleAsync(cancellationToken));
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-MIGRATIONS-APPLY-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetMigrations", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetAppliedMigrations", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetPendingMigrations", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.HasPendingModelChanges", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolated", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SqlQuery", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AddColumn", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateIndex", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.InsertData", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.RenameColumn", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropIndex", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.UpdateData", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DeleteData", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropColumn", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateTable", "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder`1", "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder`1.PrimaryKey", "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder`1.ForeignKey", "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder`1.UniqueConstraint", "Microsoft.EntityFrameworkCore.Migrations.Operations.Builders.CreateTableBuilder`1.CheckConstraint", "Microsoft.EntityFrameworkCore.Migrations.ReferentialAction", "Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Restrict", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropTable", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.RenameTable", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.Sql", "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute", "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute..ctor", "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute.Id", "Microsoft.EntityFrameworkCore.Migrations.Migration.TargetModel", "Microsoft.EntityFrameworkCore.Migrations.Migration.UpOperations", "Microsoft.EntityFrameworkCore.Migrations.Migration.DownOperations")]
    public async Task Migrate_AppliesMigrationAndPersistsValuesSynchronously()
    {
        ProviderTestRuntime.RequireCapability("migrations");
        var id = Guid.NewGuid();
        const int expected = 271828;

        try
        {
            await using (var migrate = ProviderTestRuntime.CreateContext())
            {
                await migrate.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                var migrationIds = new[] { "20261029000101_EF10Probe", "20261029000102_EF10MigrationOperations", "20261029000103_EF10MigrationChanges", "20261029000104_EF10MigrationRenameTable", "20261029000105_EF10MigrationSqlOperation", "20261029000106_EF10MigrationIndexRename", "20261029000107_EF10MigrationCheckConstraint", "20261029000108_EF10MigrationAddConstraints", "20261030000103_EF10AlterDatabaseCollation" };
                Assert.Equal(migrationIds, migrate.Database.GetMigrations());
                var portableMigrations = migrationIds.Take(5).ToArray();
                migrate.Database.Migrate(portableMigrations[^1]);
                Assert.True(migrate.Database.HasPendingModelChanges());
                Assert.Equal(portableMigrations, migrate.Database.GetAppliedMigrations());
                Assert.Equal(migrationIds.Skip(portableMigrations.Length), migrate.Database.GetPendingMigrations());
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationprobe_renamed (id, value, description) VALUES ({id}, {expected}, {"sync row"})"));
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationchild (id, probeid, code) VALUES ({Guid.NewGuid()}, {id}, {"child-sync"})"));
                Assert.Equal(4242, migrate.Database.SqlQuery<int>(
                    $"SELECT value AS Value FROM ef10conformance_migrationsql WHERE id = {1}")
                    .Single());
                Assert.ThrowsAny<DbException>(() => migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationchild (id, probeid, code) VALUES ({Guid.NewGuid()}, {id}, {string.Empty})"));
                Assert.ThrowsAny<DbException>(() => migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationchild (id, probeid, code) VALUES ({Guid.NewGuid()}, {id}, {"child-sync"})"));
                Assert.ThrowsAny<DbException>(() => migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationchild (id, probeid, code) VALUES ({Guid.NewGuid()}, {Guid.NewGuid()}, {"orphan-sync"})"));
                Assert.Equal(1, migrate.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS Value FROM ef10conformance_migrationchild child INNER JOIN ef10conformance_migrationprobe_renamed probe ON child.probeid = probe.id WHERE probe.id = {id}")
                    .Single());
                Assert.ThrowsAny<DbException>(() => migrate.Database.ExecuteSqlInterpolated(
                    $"DELETE FROM ef10conformance_migrationprobe_renamed WHERE id = {id}"));
            }

            await using (var verify = ProviderTestRuntime.CreateContext())
            {
                Assert.Equal(expected, verify.Database.SqlQuery<int>(
                        $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {id}")
                    .Single());
                Assert.Equal("sync row", verify.Database.SqlQuery<string>(
                        $"SELECT description AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {id}")
                    .Single());
                Assert.Equal(314159, verify.Database.SqlQuery<int>(
                        $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520")}")
                    .Single());
                Assert.Equal(1, verify.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS Value FROM ef10conformance_migrationchild WHERE probeid = {id}")
                    .Single());
            }

            await using (var rollback = ProviderTestRuntime.CreateContext())
            {
                rollback.Database.Migrate("20261029000101_EF10Probe");
                Assert.Equal(new[] { "20261029000101_EF10Probe" }, rollback.Database.GetAppliedMigrations());
            }

            await using (var verifyRollback = ProviderTestRuntime.CreateContext())
            {
                Assert.Equal(0, verifyRollback.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS Value FROM ef10conformance_migrationprobe WHERE id = {Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520")}")
                    .Single());
                Assert.Equal(1, verifyRollback.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS Value FROM ef10conformance_migrationprobe WHERE id = {id}")
                    .Single());
            }

            await using (var reapply = ProviderTestRuntime.CreateContext())
            {
                reapply.Database.Migrate("20261029000105_EF10MigrationSqlOperation");
            }

            await using var verifyReapply = ProviderTestRuntime.CreateContext();
            Assert.Equal(314159, verifyReapply.Database.SqlQuery<int>(
                    $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520")}")
                .Single());
            Assert.Equal(4242, verifyReapply.Database.SqlQuery<int>(
                    $"SELECT value AS Value FROM ef10conformance_migrationsql WHERE id = {1}")
                .Single());
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    [Fact]
    [CapabilityRequirement("migration-rename-index")]
    [CapabilityRequirement("migration-check-constraint")]
    [BehavioralCoverage("EF10-MIGRATIONS-INDEX-RENAME-AND-CHECK-CONSTRAINT-ASYNC", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.RenameIndex", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AddCheckConstraint", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropCheckConstraint")]
    public async Task MigrationIndexRenameAndCheckConstraint_AffectDatabaseBehaviorAsync()
    {
        ProviderTestRuntime.RequireCapability("migration-rename-index");
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520");
        try
        {
            await using (var migrate = ProviderTestRuntime.CreateContext())
            {
                await migrate.Database.EnsureDeletedAsync(cancellationToken);
                await migrate.Database.MigrateAsync("20261029000107_EF10MigrationCheckConstraint", cancellationToken);
                await Assert.ThrowsAnyAsync<DbException>(() => migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE ef10conformance_migrationprobe_renamed SET value = {-1} WHERE id = {id}", cancellationToken));
            }

            await using (var rollback = ProviderTestRuntime.CreateContext())
            {
                await rollback.Database.MigrateAsync("20261029000105_EF10MigrationSqlOperation", cancellationToken);
                Assert.Equal(314159, await rollback.Database.SqlQuery<int>(
                    $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {id}")
                    .SingleAsync(cancellationToken));
                Assert.Equal(1, await rollback.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE ef10conformance_migrationprobe_renamed SET value = {-1} WHERE id = {id}", cancellationToken));
            }
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    [Fact]
    [CapabilityRequirement("migration-rename-index")]
    [CapabilityRequirement("migration-check-constraint")]
    [BehavioralCoverage("EF10-MIGRATIONS-INDEX-RENAME-AND-CHECK-CONSTRAINT-SYNC", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.RenameIndex", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AddCheckConstraint", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropCheckConstraint")]
    public async Task MigrationIndexRenameAndCheckConstraint_AffectDatabaseBehaviorSynchronously()
    {
        ProviderTestRuntime.RequireCapability("migration-rename-index");
        var id = Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520");
        try
        {
            await using (var migrate = ProviderTestRuntime.CreateContext())
            {
                await migrate.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                migrate.Database.Migrate("20261029000107_EF10MigrationCheckConstraint");
                Assert.ThrowsAny<DbException>(() => migrate.Database.ExecuteSqlInterpolated(
                    $"UPDATE ef10conformance_migrationprobe_renamed SET value = {-1} WHERE id = {id}"));
            }

            await using (var rollback = ProviderTestRuntime.CreateContext())
            {
                rollback.Database.Migrate("20261029000105_EF10MigrationSqlOperation");
                Assert.Equal(314159, rollback.Database.SqlQuery<int>(
                    $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {id}")
                    .Single());
                Assert.Equal(1, rollback.Database.ExecuteSqlInterpolated(
                    $"UPDATE ef10conformance_migrationprobe_renamed SET value = {-1} WHERE id = {id}"));
            }
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    [Fact]
    [CapabilityRequirement("migration-add-constraint-operations")]
    [CapabilityRequirement("schema-comments")]
    [BehavioralCoverage(
        "EF10-MIGRATIONS-ADD-CONSTRAINTS-ASYNC",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AddPrimaryKey",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AddUniqueConstraint",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AddForeignKey",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateCheckConstraint",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AlterTable",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropPrimaryKey",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropUniqueConstraint",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropForeignKey", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateCheckConstraint", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AlterTable", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AlterColumn", "Microsoft.EntityFrameworkCore.Migrations.Migration.ActiveProvider", "Microsoft.EntityFrameworkCore.Migrations.Migration.InitialDatabase", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.ActiveProvider", "Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade", "Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetNull", "Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.NoAction")]
    public async Task AddConstraintMigration_EnforcesKeysAndRelationshipsAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("migration-add-constraint-operations");
        ProviderTestRuntime.RequireCapability("schema-comments");
        var cancellationToken = TestContext.Current.CancellationToken;
        var principalId = Guid.NewGuid();
        var nullablePrincipalId = Guid.NewGuid();
        var noActionPrincipalId = Guid.NewGuid();
        var seedId = Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520");
        try
        {
            await using (var migrate = ProviderTestRuntime.CreateContext())
            {
                await migrate.Database.EnsureDeletedAsync(cancellationToken);
                await migrate.Database.MigrateAsync(cancellationToken: cancellationToken);
                await using (var metadataConnection = ProviderTestRuntime.CreateConnection())
                {
                    await metadataConnection.OpenAsync(cancellationToken);
                    var tables = metadataConnection.GetSchema("Tables");
                    Assert.Contains(tables.Rows.Cast<DataRow>(), row => HasMetadataValues(
                        row, "ef10conformance_migrationconstraints", "EF10 migration constraints table"));
                }
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationprobe_renamed (id, value, description) VALUES ({principalId}, {777}, {"cascade principal"})",
                    cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationprobe_renamed (id, value, description) VALUES ({nullablePrincipalId}, {778}, {"nullable principal"})",
                    cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationprobe_renamed (id, value, description) VALUES ({noActionPrincipalId}, {779}, {"no-action principal"})",
                    cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE ef10conformance_migrationprobe_renamed SET value = {3000000000L} WHERE id = {principalId}", cancellationToken));
                Assert.Equal(3000000000L, await migrate.Database.SqlQuery<long>(
                    $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {principalId}")
                    .SingleAsync(cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE ef10conformance_migrationprobe_renamed SET value = {777} WHERE id = {principalId}", cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationconstraints (id, probeid, nullableprobeid, noactionprobeid, code) VALUES ({1}, {principalId}, {null}, {null}, {"first"})",
                    cancellationToken));
                await Assert.ThrowsAnyAsync<DbException>(() => migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationconstraints (id, probeid, nullableprobeid, noactionprobeid, code) VALUES ({2}, {principalId}, {null}, {null}, {"first"})",
                    cancellationToken));
                await Assert.ThrowsAnyAsync<DbException>(() => migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationconstraints (id, probeid, nullableprobeid, noactionprobeid, code) VALUES ({3}, {Guid.NewGuid()}, {null}, {null}, {"orphan"})",
                    cancellationToken));
                await Assert.ThrowsAnyAsync<DbException>(() => migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationconstraints (id, probeid, nullableprobeid, noactionprobeid, code) VALUES ({4}, {principalId}, {null}, {null}, {string.Empty})",
                    cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationconstraints (id, probeid, nullableprobeid, noactionprobeid, code) VALUES ({5}, {seedId}, {nullablePrincipalId}, {null}, {"set-null"})",
                    cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationconstraints (id, probeid, nullableprobeid, noactionprobeid, code) VALUES ({6}, {seedId}, {null}, {noActionPrincipalId}, {"no-action"})",
                    cancellationToken));
                await Assert.ThrowsAnyAsync<DbException>(() => migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM ef10conformance_migrationprobe_renamed WHERE id = {noActionPrincipalId}", cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM ef10conformance_migrationprobe_renamed WHERE id = {principalId}",
                    cancellationToken));
                Assert.Equal(0, await migrate.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS Value FROM ef10conformance_migrationconstraints WHERE id = {1}")
                    .SingleAsync(cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM ef10conformance_migrationprobe_renamed WHERE id = {nullablePrincipalId}", cancellationToken));
                Assert.Equal(1, await migrate.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS Value FROM ef10conformance_migrationconstraints WHERE id = {5} AND nullableprobeid IS NULL")
                    .SingleAsync(cancellationToken));
            }

            await using var rollback = ProviderTestRuntime.CreateContext();
            await rollback.Database.MigrateAsync("20261029000107_EF10MigrationCheckConstraint", cancellationToken);
            Assert.Equal(314159, await rollback.Database.SqlQuery<int>(
                $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520")}")
                .SingleAsync(cancellationToken));
            await rollback.Database.MigrateAsync(Migration.InitialDatabase, cancellationToken);
            Assert.Empty(await rollback.Database.GetAppliedMigrationsAsync(cancellationToken));
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    [Fact]
    [CapabilityRequirement("migration-add-constraint-operations")]
    [CapabilityRequirement("schema-comments")]
    [BehavioralCoverage(
        "EF10-MIGRATIONS-ADD-CONSTRAINTS-SYNC",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AddPrimaryKey",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AddUniqueConstraint",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AddForeignKey",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateCheckConstraint",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AlterTable",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropPrimaryKey",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropUniqueConstraint",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropForeignKey", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateCheckConstraint", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AlterTable", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AlterColumn", "Microsoft.EntityFrameworkCore.Migrations.Migration.ActiveProvider", "Microsoft.EntityFrameworkCore.Migrations.Migration.InitialDatabase", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.ActiveProvider", "Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.Cascade", "Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.SetNull", "Microsoft.EntityFrameworkCore.Migrations.ReferentialAction.NoAction")]
    public async Task AddConstraintMigration_EnforcesKeysAndRelationshipsSynchronously()
    {
        ProviderTestRuntime.RequireCapability("migration-add-constraint-operations");
        ProviderTestRuntime.RequireCapability("schema-comments");
        var principalId = Guid.NewGuid();
        var nullablePrincipalId = Guid.NewGuid();
        var noActionPrincipalId = Guid.NewGuid();
        var seedId = Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520");
        try
        {
            await using (var migrate = ProviderTestRuntime.CreateContext())
            {
                await migrate.Database.EnsureDeletedAsync(TestContext.Current.CancellationToken);
                migrate.Database.Migrate();
                await using (var metadataConnection = ProviderTestRuntime.CreateConnection())
                {
                    metadataConnection.Open();
                    var tables = metadataConnection.GetSchema("Tables");
                    Assert.Contains(tables.Rows.Cast<DataRow>(), row => HasMetadataValues(
                        row, "ef10conformance_migrationconstraints", "EF10 migration constraints table"));
                }
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationprobe_renamed (id, value, description) VALUES ({principalId}, {777}, {"cascade principal"})"));
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationprobe_renamed (id, value, description) VALUES ({nullablePrincipalId}, {778}, {"nullable principal"})"));
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationprobe_renamed (id, value, description) VALUES ({noActionPrincipalId}, {779}, {"no-action principal"})"));
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"UPDATE ef10conformance_migrationprobe_renamed SET value = {3000000000L} WHERE id = {principalId}"));
                Assert.Equal(3000000000L, migrate.Database.SqlQuery<long>(
                    $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {principalId}")
                    .Single());
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"UPDATE ef10conformance_migrationprobe_renamed SET value = {777} WHERE id = {principalId}"));
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationconstraints (id, probeid, nullableprobeid, noactionprobeid, code) VALUES ({1}, {principalId}, {null}, {null}, {"first"})"));
                Assert.ThrowsAny<DbException>(() => migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationconstraints (id, probeid, nullableprobeid, noactionprobeid, code) VALUES ({2}, {principalId}, {null}, {null}, {"first"})"));
                Assert.ThrowsAny<DbException>(() => migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationconstraints (id, probeid, nullableprobeid, noactionprobeid, code) VALUES ({3}, {Guid.NewGuid()}, {null}, {null}, {"orphan"})"));
                Assert.ThrowsAny<DbException>(() => migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationconstraints (id, probeid, nullableprobeid, noactionprobeid, code) VALUES ({4}, {principalId}, {null}, {null}, {string.Empty})"));
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationconstraints (id, probeid, nullableprobeid, noactionprobeid, code) VALUES ({5}, {seedId}, {nullablePrincipalId}, {null}, {"set-null"})"));
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationconstraints (id, probeid, nullableprobeid, noactionprobeid, code) VALUES ({6}, {seedId}, {null}, {noActionPrincipalId}, {"no-action"})"));
                Assert.ThrowsAny<DbException>(() => migrate.Database.ExecuteSqlInterpolated(
                    $"DELETE FROM ef10conformance_migrationprobe_renamed WHERE id = {noActionPrincipalId}"));
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"DELETE FROM ef10conformance_migrationprobe_renamed WHERE id = {principalId}"));
                Assert.Equal(0, migrate.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS Value FROM ef10conformance_migrationconstraints WHERE id = {1}")
                    .Single());
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"DELETE FROM ef10conformance_migrationprobe_renamed WHERE id = {nullablePrincipalId}"));
                Assert.Equal(1, migrate.Database.SqlQuery<int>(
                    $"SELECT COUNT(*) AS Value FROM ef10conformance_migrationconstraints WHERE id = {5} AND nullableprobeid IS NULL")
                    .Single());
            }

            await using var rollback = ProviderTestRuntime.CreateContext();
            rollback.Database.Migrate("20261029000107_EF10MigrationCheckConstraint");
            Assert.Equal(314159, rollback.Database.SqlQuery<int>(
                $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520")}")
                .Single());
            rollback.Database.Migrate(Migration.InitialDatabase);
            Assert.Empty(rollback.Database.GetAppliedMigrations());
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    private static bool HasMetadataValues(DataRow row, params string[] expectedValues) =>
        expectedValues.All(expected => row.ItemArray.Any(value => string.Equals(
            Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            expected,
            StringComparison.OrdinalIgnoreCase)));
}
