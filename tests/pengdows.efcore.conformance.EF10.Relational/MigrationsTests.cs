using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("migrations")]
public sealed class MigrationsTests
{
    [Fact]
    [BehavioralCoverage("EF10-MIGRATIONS-APPLY-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetMigrations", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetAppliedMigrationsAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetPendingMigrationsAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.HasPendingModelChanges", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SqlQuery", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AddColumn", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateIndex", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.InsertData", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.RenameColumn", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropIndex", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.UpdateData", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DeleteData", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropColumn", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateTable", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropTable", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.RenameTable", "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute", "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute..ctor", "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute.Id", "Microsoft.EntityFrameworkCore.Migrations.Migration.TargetModel", "Microsoft.EntityFrameworkCore.Migrations.Migration.UpOperations", "Microsoft.EntityFrameworkCore.Migrations.Migration.DownOperations")]
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
                var migrationIds = new[] { "20261029000101_EF10Probe", "20261029000102_EF10MigrationOperations", "20261029000103_EF10MigrationChanges", "20261029000104_EF10MigrationRenameTable" };
                Assert.Equal(migrationIds, migrate.Database.GetMigrations());
                await migrate.Database.MigrateAsync(cancellationToken);
                Assert.True(migrate.Database.HasPendingModelChanges());
                Assert.Equal(migrationIds, await migrate.Database.GetAppliedMigrationsAsync(cancellationToken));
                Assert.Empty(await migrate.Database.GetPendingMigrationsAsync(cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationprobe_renamed (id, value, description) VALUES ({id}, {expected}, {"async row"})",
                    cancellationToken));
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
                await reapply.Database.MigrateAsync(cancellationToken: cancellationToken);
            }

            await using var verifyReapply = ProviderTestRuntime.CreateContext();
            Assert.Equal(314159, await verifyReapply.Database.SqlQuery<int>(
                    $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520")}")
                .SingleAsync(cancellationToken));
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-MIGRATIONS-APPLY-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetMigrations", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetAppliedMigrations", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetPendingMigrations", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.HasPendingModelChanges", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolated", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SqlQuery", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AddColumn", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateIndex", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.InsertData", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.RenameColumn", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropIndex", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.UpdateData", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DeleteData", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropColumn", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateTable", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropTable", "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.RenameTable", "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute", "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute..ctor", "Microsoft.EntityFrameworkCore.Migrations.MigrationAttribute.Id", "Microsoft.EntityFrameworkCore.Migrations.Migration.TargetModel", "Microsoft.EntityFrameworkCore.Migrations.Migration.UpOperations", "Microsoft.EntityFrameworkCore.Migrations.Migration.DownOperations")]
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
                var migrationIds = new[] { "20261029000101_EF10Probe", "20261029000102_EF10MigrationOperations", "20261029000103_EF10MigrationChanges", "20261029000104_EF10MigrationRenameTable" };
                Assert.Equal(migrationIds, migrate.Database.GetMigrations());
                migrate.Database.Migrate();
                Assert.True(migrate.Database.HasPendingModelChanges());
                Assert.Equal(migrationIds, migrate.Database.GetAppliedMigrations());
                Assert.Empty(migrate.Database.GetPendingMigrations());
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationprobe_renamed (id, value, description) VALUES ({id}, {expected}, {"sync row"})"));
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
                reapply.Database.Migrate();
            }

            await using var verifyReapply = ProviderTestRuntime.CreateContext();
            Assert.Equal(314159, verifyReapply.Database.SqlQuery<int>(
                    $"SELECT value AS Value FROM ef10conformance_migrationprobe_renamed WHERE id = {Guid.Parse("3a34d6d8-aef0-42af-a248-28a2d82a6520")}")
                .Single());
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }
}
