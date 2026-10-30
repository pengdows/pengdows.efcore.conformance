using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("migrations")]
public sealed class SequenceMigrationTests
{
    [Fact]
    [CapabilityRequirement("sequences")]
    [BehavioralCoverage(
        "EF10-MIGRATION-SEQUENCE-OPERATIONS",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.EnsureSchema",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.CreateSequence",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AlterSequence",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.RestartSequence",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.RenameSequence",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropSequence",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.DropSchema")]
    public async Task SequenceMigrations_CreateAlterRestartRenameAndDropSequence()
    {
        ProviderTestRuntime.RequireCapability("sequences");
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            await using (var migrate = ProviderTestRuntime.CreateContext())
            {
                await migrate.Database.EnsureDeletedAsync(cancellationToken);
                await migrate.Database.MigrateAsync("20261030000104_EF10SequenceOperations", cancellationToken);

                var firstValue = await migrate.Database.SqlQuery<long>(
                        $"SELECT nextval(format('%I.%I', {Ef10SequenceOperationsMigration.SchemaName}, 'ef10conformance_sequence_renamed')) AS \"Value\"")
                    .SingleAsync(cancellationToken);
                var schemaExists = await migrate.Database.SqlQuery<long>(
                        $"SELECT COUNT(*)::bigint AS \"Value\" FROM pg_namespace WHERE nspname = {Ef10SequenceOperationsMigration.SchemaName}")
                    .SingleAsync(cancellationToken);

                Assert.Equal(25L, firstValue);
                Assert.Equal(1L, schemaExists);
            }

            await using (var rollback = ProviderTestRuntime.CreateContext())
            {
                await rollback.Database.MigrateAsync("20261030000103_EF10AlterDatabaseCollation", cancellationToken);
                var sequenceCount = await rollback.Database.SqlQuery<long>(
                        $"SELECT COUNT(*)::bigint AS \"Value\" FROM pg_sequences WHERE schemaname = {Ef10SequenceOperationsMigration.SchemaName}")
                    .SingleAsync(cancellationToken);
                var schemaCount = await rollback.Database.SqlQuery<long>(
                        $"SELECT COUNT(*)::bigint AS \"Value\" FROM pg_namespace WHERE nspname = {Ef10SequenceOperationsMigration.SchemaName}")
                    .SingleAsync(cancellationToken);

                Assert.Equal(0L, sequenceCount);
                Assert.Equal(0L, schemaCount);
            }
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }
}
