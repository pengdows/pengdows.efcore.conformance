using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.Npgsql;

/// <summary>EF Core 10 adapter for PostgreSQL using Npgsql.</summary>
public sealed class NpgsqlAdapter : IAdoNetProviderAdapter, IParameterizedCollectionModeAdapter, IIndexMetadataAdapter, ICollationTestAdapter, ISchemaCommentMetadataAdapter
{
    private static readonly ProviderCapabilities DeclaredCapabilities = new(
        new HashSet<string>(StringComparer.Ordinal)
        {
            "relational", "transactions", "ambient-transactions", "savepoints", "migrations",
            "filtered-indexes", "json-mapping", "raw-sql", "execute-update", "execute-delete",
            "primitive-collections", "external-connection", "parameterized-collection-modes",
            "least-greatest-functions", "migration-rename-index", "migration-check-constraint",
            "index-schema-metadata", "migration-add-constraint-operations", "migration-set-default",
            "index-sort-metadata", "sequences", "schemas", "schema-qualified-functions", "case-insensitive-collation", "schema-comments"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "database-collation-migration"
        });

    public string Name => "Npgsql EF Core PostgreSQL";

    public string CaseInsensitiveCollation => "ef10conformance_ci";

    public string Version => typeof(NpgsqlConnection).Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

    public void Configure(DbContextOptionsBuilder options, string connectionString) => options.UseNpgsql(connectionString);

    public void ConfigureParameterizedCollectionMode(
        DbContextOptionsBuilder options,
        string connectionString,
        ParameterTranslationMode mode) =>
        options.UseNpgsql(connectionString, npgsql => npgsql.UseParameterizedCollectionMode(mode));

    public async Task InitializeDatabaseAsync(DbContext context, CancellationToken cancellationToken)
    {
        await context.Database.EnsureCreatedAsync(cancellationToken);
        await context.Database.ExecuteSqlRawAsync(
            "CREATE COLLATION IF NOT EXISTS ef10conformance_ci (provider = icu, locale = 'und-u-ks-level2', deterministic = false)",
            cancellationToken);
    }

    public Task CleanupDatabaseAsync(DbContext context, CancellationToken cancellationToken) =>
        context.Database.EnsureDeletedAsync(cancellationToken);

    public DbConnection CreateConnection(string connectionString) => new NpgsqlConnection(connectionString);

    public IReadOnlyList<IndexColumnMetadata> GetIndexColumns(DbConnection openConnection, string tableName, string indexName)
    {
        using var command = openConnection.CreateCommand();
        command.CommandText = """
            SELECT a.attname, k.ordinality, (i.indoption[k.ordinality - 1] & 1) = 1
            FROM pg_index i
            JOIN pg_class tbl ON tbl.oid = i.indrelid
            JOIN pg_class idx ON idx.oid = i.indexrelid
            CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ordinality)
            JOIN pg_attribute a ON a.attrelid = tbl.oid AND a.attnum = k.attnum
            WHERE tbl.relname = @tableName AND idx.relname = @indexName
            ORDER BY k.ordinality
            """;
        AddParameter(command, "tableName", tableName);
        AddParameter(command, "indexName", indexName);
        using var reader = command.ExecuteReader();
        var columns = new List<IndexColumnMetadata>();
        while (reader.Read())
        {
            columns.Add(new IndexColumnMetadata(reader.GetString(0), reader.GetInt32(1), reader.GetBoolean(2)));
        }

        return columns;
    }

    public string? GetTableComment(DbConnection openConnection, string tableName) => ReadOptionalComment(
        openConnection,
        """
        SELECT obj_description(table_relation.oid, 'pg_class')
        FROM pg_class AS table_relation
        JOIN pg_namespace AS schema_namespace ON schema_namespace.oid = table_relation.relnamespace
        WHERE schema_namespace.nspname = current_schema() AND table_relation.relname = @tableName
        """,
        ("tableName", tableName));

    public string? GetColumnComment(DbConnection openConnection, string tableName, string columnName) => ReadOptionalComment(
        openConnection,
        """
        SELECT col_description(table_relation.oid, table_column.attnum)
        FROM pg_class AS table_relation
        JOIN pg_namespace AS schema_namespace ON schema_namespace.oid = table_relation.relnamespace
        JOIN pg_attribute AS table_column ON table_column.attrelid = table_relation.oid
        WHERE schema_namespace.nspname = current_schema()
          AND table_relation.relname = @tableName AND table_column.attname = @columnName
        """,
        ("tableName", tableName),
        ("columnName", columnName));

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string? ReadOptionalComment(DbConnection openConnection, string sql, params (string Name, string Value)[] values)
    {
        using var command = openConnection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in values)
        {
            AddParameter(command, name, value);
        }

        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
