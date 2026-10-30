using System.Data.Common;
using DuckDB.NET.Data;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.duckdb;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.PengdowsDuckDb;

/// <summary>Candidate adapter for the pengdows ADO.NET DuckDB EF provider.</summary>
public sealed class PengdowsDuckDbAdapter : IAdoNetProviderAdapter, IParameterizedCollectionModeAdapter
{
    private static readonly ProviderCapabilities DeclaredCapabilities = new(
        new HashSet<string>(StringComparer.Ordinal)
        {
            "relational", "transactions", "savepoints", "migrations", "raw-sql", "execute-update",
            "execute-delete", "external-connection", "parameterized-collection-modes", "native-type-fidelity"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "ambient-transactions", "filtered-indexes", "json-mapping", "primitive-collections",
            "least-greatest-functions", "migration-rename-index", "migration-check-constraint",
            "index-schema-metadata", "schema-comments", "migration-add-constraint-operations",
            "case-insensitive-collation", "migration-set-default", "index-sort-metadata", "sequences",
            "schemas", "database-collation-migration", "schema-qualified-functions"
        });

    public string Name => "pengdows.efcore.adonet DuckDB";

    public string Version => typeof(DuckDbDbContextOptionsBuilderExtensions).Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

    public void Configure(DbContextOptionsBuilder options, string connectionString) => options.UseAdoNetDuckDb(connectionString);

    public void ConfigureParameterizedCollectionMode(DbContextOptionsBuilder options, string connectionString, ParameterTranslationMode mode) =>
        options.UseAdoNetDuckDb(connectionString, provider => provider.UseParameterizedCollectionMode(mode));

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
