using System.Data.Common;
using DuckDB.NET.Data;
using DuckDB.EFCore.Extensions;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.DuckDb;

/// <summary>Reference adapter for the independent DuckDB.EFCore provider.</summary>
public sealed class DuckDbReferenceAdapter : IAdoNetProviderAdapter
{
    private static readonly ProviderCapabilities DeclaredCapabilities = new(
        new HashSet<string>(StringComparer.Ordinal)
        {
            "relational", "transactions", "migrations", "raw-sql", "external-connection", "native-type-fidelity"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "ambient-transactions", "savepoints", "filtered-indexes", "json-mapping", "execute-update",
            "execute-delete", "primitive-collections", "parameterized-collection-modes", "least-greatest-functions",
            "migration-rename-index", "migration-check-constraint", "index-schema-metadata", "schema-comments",
            "migration-add-constraint-operations", "case-insensitive-collation", "migration-set-default",
            "index-sort-metadata", "sequences", "schemas", "database-collation-migration", "schema-qualified-functions"
        });

    public string Name => "DuckDB.EFCore reference";

    public string Version => typeof(DuckDBDbContextOptionsBuilderExtensions).Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

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

    public DbConnection CreateConnection(string connectionString) => new DuckDBConnection(connectionString);
}
