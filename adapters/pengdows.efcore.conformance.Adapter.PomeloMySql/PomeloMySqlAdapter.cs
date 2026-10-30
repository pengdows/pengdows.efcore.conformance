using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.PomeloMySql;

/// <summary>Optional EF Core 10 adapter for MySQL-compatible servers using Pomelo.</summary>
public sealed class PomeloMySqlAdapter : IAdoNetProviderAdapter, IParameterizedCollectionModeAdapter, ICollationTestAdapter, IIndexMetadataAdapter
{
    private static readonly ProviderCapabilities DeclaredCapabilities = new(
        new HashSet<string>(StringComparer.Ordinal)
        {
            "relational",
            "transactions",
            "ambient-transactions",
            "savepoints",
            "migrations",
            "migration-rename-index",
            "migration-check-constraint",
            "index-schema-metadata",
            "schema-comments",
            "migration-add-constraint-operations",
            "case-insensitive-collation",
            "index-sort-metadata",
            "raw-sql",
            "execute-update",
            "execute-delete",
            "external-connection",
            "parameterized-collection-modes"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "filtered-indexes",
            "json-mapping",
            "primitive-collections",
            "least-greatest-functions",
            "migration-set-default"
        });

    public string Name => "Pomelo EF Core MySQL";

    public string CaseInsensitiveCollation => "utf8mb4_0900_ai_ci";

    public string Version => typeof(Microsoft.EntityFrameworkCore.MySqlDbContextOptionsBuilderExtensions)
        .Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

    public void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)));

    public void ConfigureParameterizedCollectionMode(
        DbContextOptionsBuilder options,
        string connectionString,
        ParameterTranslationMode mode) =>
        options.UseMySql(
            connectionString,
            new MySqlServerVersion(new Version(8, 4, 0)),
            mysql => mysql.UseParameterizedCollectionMode(mode));

    public Task InitializeDatabaseAsync(DbContext context, CancellationToken cancellationToken) =>
        context.Database.EnsureCreatedAsync(cancellationToken);

    public Task CleanupDatabaseAsync(DbContext context, CancellationToken cancellationToken) =>
        context.Database.EnsureDeletedAsync(cancellationToken);

    public DbConnection CreateConnection(string connectionString) => new MySqlConnection(connectionString);

    public IReadOnlyList<IndexColumnMetadata> GetIndexColumns(DbConnection openConnection, string tableName, string indexName)
    {
        using var command = openConnection.CreateCommand();
        command.CommandText = """
            SELECT COLUMN_NAME, SEQ_IN_INDEX, COLLATION
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND INDEX_NAME = @indexName
            ORDER BY SEQ_IN_INDEX
            """;
        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@tableName";
        tableParameter.Value = tableName;
        command.Parameters.Add(tableParameter);
        var indexParameter = command.CreateParameter();
        indexParameter.ParameterName = "@indexName";
        indexParameter.Value = indexName;
        command.Parameters.Add(indexParameter);

        using var reader = command.ExecuteReader();
        var columns = new List<IndexColumnMetadata>();
        while (reader.Read())
        {
            var sortOrder = reader.IsDBNull(2) ? null : reader.GetString(2);
            columns.Add(new IndexColumnMetadata(reader.GetString(0), reader.GetInt32(1), string.Equals(sortOrder, "D", StringComparison.OrdinalIgnoreCase)));
        }

        return columns;
    }
}
