using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.sqlserver;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.PengdowsSqlServer;

/// <summary>Candidate adapter for the pengdows ADO.NET SQL Server EF provider.</summary>
public sealed class PengdowsSqlServerAdapter : IAdoNetProviderAdapter, IParameterizedCollectionModeAdapter,
    IIndexMetadataAdapter, ISchemaCommentMetadataAdapter
{
    private static readonly ProviderCapabilities DeclaredCapabilities = new(
        new HashSet<string>(StringComparer.Ordinal)
        {
            "relational", "transactions", "ambient-transactions", "savepoints", "migrations", "filtered-indexes",
            "json-mapping", "raw-sql", "execute-update", "execute-delete", "primitive-collections",
            "external-connection", "parameterized-collection-modes", "least-greatest-functions",
            "migration-rename-index", "migration-check-constraint", "index-schema-metadata", "schema-comments",
            "migration-add-constraint-operations", "case-insensitive-collation", "migration-set-default",
            "index-sort-metadata", "sequences", "schemas", "database-collation-migration",
            "schema-qualified-functions", "native-type-fidelity"
        },
        new HashSet<string>(StringComparer.Ordinal));

    public string Name => "pengdows.efcore.adonet SQL Server";

    public string Version => typeof(SqlServerDbContextOptionsBuilderExtensions).Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

    public void Configure(DbContextOptionsBuilder options, string connectionString) => options.UseAdoNetSqlServer(connectionString);

    public void ConfigureParameterizedCollectionMode(DbContextOptionsBuilder options, string connectionString, ParameterTranslationMode mode) =>
        options.UseAdoNetSqlServer(connectionString, provider => provider.UseParameterizedCollectionMode(mode));

    public Task InitializeDatabaseAsync(DbContext context, CancellationToken cancellationToken) => context.Database.EnsureCreatedAsync(cancellationToken);

    public Task CleanupDatabaseAsync(DbContext context, CancellationToken cancellationToken) => context.Database.EnsureDeletedAsync(cancellationToken);

    public DbConnection CreateConnection(string connectionString) => new SqlConnection(connectionString);

    public IReadOnlyList<IndexColumnMetadata> GetIndexColumns(DbConnection openConnection, string tableName, string indexName)
    {
        using var command = openConnection.CreateCommand();
        command.CommandText = """
            SELECT c.name, ic.key_ordinal, ic.is_descending_key
            FROM sys.indexes AS i
            INNER JOIN sys.index_columns AS ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            INNER JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(@tableName) AND i.name = @indexName AND ic.key_ordinal > 0
            ORDER BY ic.key_ordinal
            """;
        AddParameter(command, "@tableName", tableName);
        AddParameter(command, "@indexName", indexName);
        using var reader = command.ExecuteReader();
        var columns = new List<IndexColumnMetadata>();
        while (reader.Read()) columns.Add(new IndexColumnMetadata(reader.GetString(0), reader.GetInt16(1), reader.GetBoolean(2)));
        return columns;
    }

    public string? GetTableComment(DbConnection openConnection, string tableName) => ReadComment(
        openConnection,
        "SELECT CAST(ep.value AS nvarchar(max)) FROM sys.extended_properties AS ep WHERE ep.name = N'MS_Description' AND ep.major_id = OBJECT_ID(@tableName) AND ep.minor_id = 0",
        ("@tableName", tableName));

    public string? GetColumnComment(DbConnection openConnection, string tableName, string columnName) => ReadComment(
        openConnection,
        "SELECT CAST(ep.value AS nvarchar(max)) FROM sys.extended_properties AS ep WHERE ep.name = N'MS_Description' AND ep.major_id = OBJECT_ID(@tableName) AND ep.minor_id = COLUMNPROPERTY(OBJECT_ID(@tableName), @columnName, 'ColumnId')",
        ("@tableName", tableName), ("@columnName", columnName));

    private static string? ReadComment(DbConnection connection, string sql, params (string Name, string Value)[] values)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in values) AddParameter(command, name, value);
        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
