using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("migrations")]
public sealed class MigrationFailureBoundaryTests
{
    private const string FirstMigration = "20261111000101_EF10MigrationTransactionPrior";
    private const string FailingMigration = "20261111000102_EF10MigrationTransactionFailure";

    [Fact]
    [BehavioralCoverage("EF10-MIGRATION-SET-FAILURE-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.MigrateAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetAppliedMigrationsAsync")]
    public async Task FailedLaterMigration_LeavesEarlierMigrationAndHistoryPersistedAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("migrations");
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            await using (var migrate = CreateContext())
            {
                await migrate.Database.EnsureDeletedAsync(cancellationToken);
                await Assert.ThrowsAnyAsync<DbException>(() => migrate.Database.MigrateAsync(FailingMigration, cancellationToken));
            }

            Assert.Equal(new[] { FirstMigration }, await ReadHistoryAsync(cancellationToken));
            Assert.Equal(8675309, await ReadFirstMigrationValueAsync(cancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    [Fact]
    [BehavioralCoverage("EF10-MIGRATION-SET-FAILURE-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.Migrate", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.GetAppliedMigrations")]
    public async Task FailedLaterMigration_LeavesEarlierMigrationAndHistoryPersistedSynchronously()
    {
        ProviderTestRuntime.RequireCapability("migrations");

        try
        {
            await using (var migrate = CreateContext())
            {
                migrate.Database.EnsureDeleted();
                Assert.ThrowsAny<DbException>(() => migrate.Database.Migrate(FailingMigration));
            }

            Assert.Equal(new[] { FirstMigration }, await ReadHistoryAsync(TestContext.Current.CancellationToken));
            Assert.Equal(8675309, await ReadFirstMigrationValueAsync(TestContext.Current.CancellationToken));
        }
        finally
        {
            await using var cleanup = CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }

    private static MigrationFailureProbeContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MigrationFailureProbeContext>();
        ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString);
        return new MigrationFailureProbeContext(options.Options);
    }

    private static async Task<string[]> ReadHistoryAsync(CancellationToken cancellationToken)
    {
        await using var connection = ProviderTestRuntime.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var ids = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetString(0));
        }

        return ids.ToArray();
    }

    private static async Task<int> ReadFirstMigrationValueAsync(CancellationToken cancellationToken)
    {
        await using var connection = ProviderTestRuntime.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM ef10conformance_migrationtransaction WHERE id = 1";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed class MigrationFailureProbeContext(DbContextOptions<MigrationFailureProbeContext> options) : DbContext(options);
