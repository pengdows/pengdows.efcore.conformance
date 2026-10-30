using System.Data.Common;
using DuckDB.NET.Data;
using DuckDB.EFCoreProvider.Extensions;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.SkuirrelsDuckDb;

/// <summary>Reference adapter for skuirrels' DuckDB.EFCoreProvider (EF Core 10).</summary>
public sealed class SkuirrelsDuckDbReferenceAdapter : IAdoNetProviderAdapter, INativeTypeMappingProfile, INativeTypeTableCleanupAdapter
{
    private static readonly IReadOnlyList<NativeTypeMapping> DeclaredNativeTypes =
    [
        new("BOOLEAN", typeof(bool)),
        new("SMALLINT", typeof(short)),
        new("INTEGER", typeof(int)),
        new("BIGINT", typeof(long)),
        new("REAL", typeof(float)),
        new("DOUBLE", typeof(double)),
        new("DECIMAL", typeof(decimal), "DECIMAL(38,4)"),
        new("VARCHAR", typeof(string)),
        new("BLOB", typeof(byte[])),
        new("DATE", typeof(DateOnly)),
        new("TIME", typeof(TimeOnly)),
        new("TIMESTAMP", typeof(DateTime)),
        new("TIMESTAMP WITH TIME ZONE", typeof(DateTimeOffset)),
        new("INTERVAL", typeof(TimeSpan)),
        new("UUID", typeof(Guid))
    ];
    private static readonly ProviderCapabilities DeclaredCapabilities = new(
        new HashSet<string>(StringComparer.Ordinal)
        {
            "relational", "transactions", "migrations", "raw-sql", "external-connection", "native-type-fidelity",
            "savepoints", "execute-update", "execute-delete"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "ambient-transactions", "filtered-indexes", "json-mapping", "primitive-collections", "parameterized-collection-modes", "least-greatest-functions",
            "migration-rename-index", "migration-check-constraint", "index-schema-metadata", "schema-comments",
            "migration-add-constraint-operations", "case-insensitive-collation", "migration-set-default",
            "index-sort-metadata", "sequences", "schemas", "database-collation-migration", "schema-qualified-functions"
        });

    public string Name => "DuckDB.EFCoreProvider (skuirrels) reference";

    public string Version => typeof(DuckDBDbContextOptionsBuilderExtensions).Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

    public IReadOnlyList<NativeTypeMapping> NativeTypeMappings => DeclaredNativeTypes;

    public void Configure(DbContextOptionsBuilder options, string connectionString) => options.UseDuckDB(connectionString);

    public Task InitializeDatabaseAsync(DbContext context, CancellationToken cancellationToken) => context.Database.EnsureCreatedAsync(cancellationToken);

    public Task CleanupDatabaseAsync(DbContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var dataSource = context.Database.GetDbConnection().ConnectionString.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=', 2))
            .FirstOrDefault(pair => pair.Length == 2 && pair[0].Trim().Equals("Data Source", StringComparison.OrdinalIgnoreCase))?[1].Trim();
        if (!string.IsNullOrWhiteSpace(dataSource) && !dataSource.Equals(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(dataSource);
        }

        return Task.CompletedTask;
    }

    public async Task CleanupNativeTypeTablesAsync(string connectionString, IReadOnlyList<string> tableNames, CancellationToken cancellationToken)
    {
        await using var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = string.Join(";", tableNames.Select(name => $"DROP TABLE IF EXISTS \"{name}\""));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public DbConnection CreateConnection(string connectionString) => new DuckDBConnection(connectionString);
}
