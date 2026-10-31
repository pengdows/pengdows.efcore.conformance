using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.MicrosoftSqlite;

/// <summary>Reference adapter using Microsoft's EF Core SQLite provider.</summary>
public sealed class MicrosoftSqliteAdapter : IAdoNetProviderAdapter, IParameterizedCollectionModeAdapter, ICollationTestAdapter, IIndexMetadataAdapter
{
    private static readonly ProviderCapabilities SupportedCapabilities = new(
        new HashSet<string>(StringComparer.Ordinal)
        {
            "relational",
            "transactions", "isolation-serializable",
            "savepoints",
            "migrations",
            "filtered-indexes",
            "json-mapping",
            "raw-sql",
            "execute-update",
            "execute-delete",
            "primitive-collections",
            "external-connection",
            "parameterized-collection-modes",
            "case-insensitive-collation",
            "migration-set-default",
            "index-sort-metadata"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "ambient-transactions",
            "least-greatest-functions",
            "migration-rename-index",
            "migration-check-constraint",
            "index-schema-metadata",
            "schema-comments",
            "migration-add-constraint-operations",
            "sequences",
            "schemas",
            "database-collation-migration",
            "schema-qualified-functions",
            "native-type-fidelity"
        });

    public string Name => "Microsoft EF Core SQLite";

    public string CaseInsensitiveCollation => "NOCASE";

    public string Version => typeof(SqliteConnection).Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => SupportedCapabilities;

    public void Configure(DbContextOptionsBuilder options, string connectionString) => options.UseSqlite(connectionString);

    public void ConfigureParameterizedCollectionMode(
        DbContextOptionsBuilder options,
        string connectionString,
        ParameterTranslationMode mode) =>
        options.UseSqlite(connectionString, sqlite => sqlite.UseParameterizedCollectionMode(mode));

    public Task InitializeDatabaseAsync(DbContext context, CancellationToken cancellationToken) =>
        context.Database.EnsureCreatedAsync(cancellationToken);

    public Task CleanupDatabaseAsync(DbContext context, CancellationToken cancellationToken) =>
        context.Database.EnsureDeletedAsync(cancellationToken);

    public DbConnection CreateConnection(string connectionString) => new SqliteConnection(connectionString);

    public IReadOnlyList<IndexColumnMetadata> GetIndexColumns(DbConnection openConnection, string tableName, string indexName)
    {
        using var command = openConnection.CreateCommand();
        var escapedName = indexName.Replace("'", "''", StringComparison.Ordinal);
        command.CommandText = $"PRAGMA index_xinfo('{escapedName}')";
        using var reader = command.ExecuteReader();
        var columns = new List<IndexColumnMetadata>();
        var ordinalIndex = reader.GetOrdinal("seqno");
        var nameIndex = reader.GetOrdinal("name");
        var descendingIndex = reader.GetOrdinal("desc");
        var keyIndex = reader.GetOrdinal("key");
        while (reader.Read())
        {
            if (reader.GetInt32(keyIndex) == 0 || reader.IsDBNull(nameIndex))
            {
                continue;
            }

            columns.Add(new IndexColumnMetadata(reader.GetString(nameIndex), reader.GetInt32(ordinalIndex), reader.GetInt32(descendingIndex) != 0));
        }

        return columns;
    }
}
