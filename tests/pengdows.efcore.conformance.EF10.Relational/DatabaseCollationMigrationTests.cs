using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("migrations")]
public sealed class DatabaseCollationMigrationTests
{
    [Fact]
    [CapabilityRequirement("database-collation-migration")]
    [BehavioralCoverage(
        "EF10-MIGRATION-DATABASE-COLLATION",
        "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.AlterDatabase")]
    public async Task AlterDatabaseMigration_ChangesDefaultCollation()
    {
        ProviderTestRuntime.RequireCapability("database-collation-migration");
        var cancellationToken = TestContext.Current.CancellationToken;

        try
        {
            await using var context = ProviderTestRuntime.CreateContext();
            await context.Database.EnsureDeletedAsync(cancellationToken);
            await context.Database.MigrateAsync("20261030000103_EF10AlterDatabaseCollation", cancellationToken);

            var actual = await context.Database.SqlQuery<string>(
                    $"SELECT DEFAULT_COLLATION_NAME AS Value FROM information_schema.SCHEMATA WHERE SCHEMA_NAME = DATABASE()")
                .SingleAsync(cancellationToken);

            Assert.Equal("utf8mb4_bin", actual);
        }
        finally
        {
            await using var cleanup = ProviderTestRuntime.CreateContext();
            await cleanup.Database.EnsureDeletedAsync(CancellationToken.None);
        }
    }
}
