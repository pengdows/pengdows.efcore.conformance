using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("migrations")]
public sealed class MigrationsTests
{
    [Fact]
    [BehavioralCoverage("EF10-MIGRATIONS-APPLY-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetMigrations", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetAppliedMigrationsAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetPendingMigrationsAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.HasPendingModelChanges", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SqlQuery")]
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
                Assert.Equal(new[] { "202610290001_EF10Probe" }, migrate.Database.GetMigrations());
                await migrate.Database.MigrateAsync(cancellationToken);
                Assert.True(migrate.Database.HasPendingModelChanges());
                Assert.Equal(new[] { "202610290001_EF10Probe" }, await migrate.Database.GetAppliedMigrationsAsync(cancellationToken));
                Assert.Empty(await migrate.Database.GetPendingMigrationsAsync(cancellationToken));
                Assert.Equal(1, await migrate.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO ef10conformance_migrationprobe (id, value) VALUES ({id}, {expected})",
                    cancellationToken));
            }

            await using var verify = ProviderTestRuntime.CreateContext();
            var actual = await verify.Database.SqlQuery<int>(
                    $"SELECT value AS Value FROM ef10conformance_migrationprobe WHERE id = {id}")
                .SingleAsync(cancellationToken);
            Assert.Equal(expected, actual);
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-MIGRATIONS-APPLY-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetMigrations", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetAppliedMigrations", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetPendingMigrations", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.HasPendingModelChanges", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolated", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SqlQuery")]
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
                Assert.Equal(new[] { "202610290001_EF10Probe" }, migrate.Database.GetMigrations());
                migrate.Database.Migrate();
                Assert.True(migrate.Database.HasPendingModelChanges());
                Assert.Equal(new[] { "202610290001_EF10Probe" }, migrate.Database.GetAppliedMigrations());
                Assert.Empty(migrate.Database.GetPendingMigrations());
                Assert.Equal(1, migrate.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO ef10conformance_migrationprobe (id, value) VALUES ({id}, {expected})"));
            }

            await using var verify = ProviderTestRuntime.CreateContext();
            var actual = verify.Database.SqlQuery<int>(
                    $"SELECT value AS Value FROM ef10conformance_migrationprobe WHERE id = {id}")
                .Single();
            Assert.Equal(expected, actual);
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }
}
