using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using pengdows.efcore.adonet.mysql;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.PengdowsMySql;

/// <summary>Candidate adapter for the pengdows ADO.NET MySQL EF provider.</summary>
public sealed class PengdowsMySqlAdapter : IAdoNetProviderAdapter, INativeTypeMappingProfile, INativeTypeTableCleanupAdapter,
    IDatabaseTypeCatalogDiscovery, IParameterizedCollectionModeAdapter, ICollationTestAdapter, IIndexMetadataAdapter,
    ISchemaCommentMetadataAdapter
{
    private static readonly IReadOnlyList<NativeTypeMapping> DeclaredNativeTypes =
    [
        new("BOOL", typeof(bool), "tinyint"),
        new("BIT", typeof(ulong), "bit", "16"),
        new("TINYINT", typeof(sbyte)),
        new("TINYINT", typeof(byte), "tinyint unsigned"),
        new("SMALLINT", typeof(short)),
        new("SMALLINT", typeof(ushort), "smallint unsigned"),
        new("INT", typeof(int)),
        new("INT", typeof(uint), "int unsigned"),
        new("MEDIUMINT", typeof(int)),
        new("MEDIUMINT", typeof(uint), "mediumint unsigned"),
        new("BIGINT", typeof(long)),
        new("BIGINT", typeof(ulong), "bigint unsigned"),
        new("DECIMAL", typeof(decimal), "decimal(65,30)"),
        new("DOUBLE", typeof(double)),
        new("FLOAT", typeof(float)),
        new("VARCHAR", typeof(string), "varchar(255)"),
        new("CHAR", typeof(string), "char(1)"),
        new("TINYTEXT", typeof(string)),
        new("TEXT", typeof(string)),
        new("MEDIUMTEXT", typeof(string)),
        new("LONGTEXT", typeof(string)),
        new("BLOB", typeof(byte[])),
        new("BINARY", typeof(byte[]), "binary(1)"),
        new("VARBINARY", typeof(byte[]), "varbinary(255)"),
        new("TINYBLOB", typeof(byte[])),
        new("MEDIUMBLOB", typeof(byte[])),
        new("LONGBLOB", typeof(byte[])),
        new("DATETIME", typeof(DateTime)),
        new("DATE", typeof(DateOnly)),
        new("DATE", typeof(DateTime), "date"),
        new("TIME", typeof(TimeSpan)),
        new("TIMESTAMP", typeof(DateTime)),
        new("YEAR", typeof(int)),
        new("GUID", typeof(Guid), "char(36)", "800")
    ];

    private static readonly ProviderCapabilities DeclaredCapabilities = new(
        new HashSet<string>(StringComparer.Ordinal)
        {
            "relational", "transactions", "ambient-transactions", "savepoints", "migrations",
            "migration-rename-index", "migration-check-constraint", "index-schema-metadata", "schema-comments",
            "migration-add-constraint-operations", "case-insensitive-collation", "index-sort-metadata",
            "database-collation-migration", "raw-sql", "execute-update", "execute-delete", "external-connection",
            "parameterized-collection-modes", "native-type-fidelity"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "filtered-indexes", "json-mapping", "primitive-collections", "least-greatest-functions",
            "migration-set-default", "sequences", "schemas", "schema-qualified-functions"
        });

    public string Name => "pengdows.efcore.adonet MySQL";

    public string CaseInsensitiveCollation => "utf8mb4_0900_ai_ci";

    public string Version => typeof(MySqlDbContextOptionsBuilderExtensions).Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

    public IReadOnlyList<NativeTypeMapping> NativeTypeMappings => DeclaredNativeTypes;

    public void Configure(DbContextOptionsBuilder options, string connectionString) => options.UseAdoNetMySql(connectionString);

    public void ConfigureParameterizedCollectionMode(DbContextOptionsBuilder options, string connectionString, ParameterTranslationMode mode) =>
        options.UseAdoNetMySql(connectionString, provider => provider.UseParameterizedCollectionMode(mode));

    public Task InitializeDatabaseAsync(DbContext context, CancellationToken cancellationToken) => context.Database.EnsureCreatedAsync(cancellationToken);

    public Task CleanupDatabaseAsync(DbContext context, CancellationToken cancellationToken) => context.Database.EnsureDeletedAsync(cancellationToken);

    public DbConnection CreateConnection(string connectionString) => new MySqlConnection(connectionString);

    public async Task CleanupNativeTypeTablesAsync(string connectionString, IReadOnlyList<string> tableNames, CancellationToken cancellationToken)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = string.Join(";", tableNames.Select(name => $"DROP TABLE IF EXISTS {QuoteTestTable(name)}"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public IReadOnlyList<IndexColumnMetadata> GetIndexColumns(DbConnection openConnection, string tableName, string indexName)
    {
        using var command = openConnection.CreateCommand();
        command.CommandText = """
            SELECT COLUMN_NAME, SEQ_IN_INDEX, COLLATION
            FROM information_schema.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND INDEX_NAME = @indexName
            ORDER BY SEQ_IN_INDEX
            """;
        AddParameter(command, "@tableName", tableName);
        AddParameter(command, "@indexName", indexName);
        using var reader = command.ExecuteReader();
        var columns = new List<IndexColumnMetadata>();
        while (reader.Read())
        {
            var sortOrder = reader.IsDBNull(2) ? null : reader.GetString(2);
            columns.Add(new IndexColumnMetadata(reader.GetString(0), reader.GetInt32(1), string.Equals(sortOrder, "D", StringComparison.OrdinalIgnoreCase)));
        }

        return columns;
    }

    public string? GetTableComment(DbConnection openConnection, string tableName) => ReadOptionalComment(
        openConnection,
        "SELECT TABLE_COMMENT FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName",
        ("tableName", tableName));

    public string? GetColumnComment(DbConnection openConnection, string tableName, string columnName) => ReadOptionalComment(
        openConnection,
        "SELECT COLUMN_COMMENT FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName",
        ("tableName", tableName),
        ("columnName", columnName));

    public DatabaseTypeCatalogSnapshot DiscoverDatabaseTypeCatalog(DbConnection openConnection)
    {
        using var command = openConnection.CreateCommand();
        command.CommandText = """
            SELECT TABLE_SCHEMA,
                   CONCAT(TABLE_NAME, '.', COLUMN_NAME),
                   DATA_TYPE,
                   COLUMN_TYPE,
                   CASE WHEN DATA_TYPE IN ('tinyint', 'smallint', 'mediumint', 'int', 'integer', 'bigint', 'decimal', 'numeric', 'float', 'double', 'real', 'bit') THEN 'numeric'
                        WHEN DATA_TYPE IN ('date', 'time', 'datetime', 'timestamp', 'year') THEN 'temporal'
                        WHEN DATA_TYPE IN ('char', 'varchar', 'tinytext', 'text', 'mediumtext', 'longtext', 'enum', 'set') THEN 'character'
                        WHEN DATA_TYPE IN ('binary', 'varbinary', 'tinyblob', 'blob', 'mediumblob', 'longblob') THEN 'binary'
                        ELSE 'other' END,
                   FALSE,
                   TRUE,
                   NULL,
                   NULL,
                   TABLE_NAME
            FROM information_schema.COLUMNS
            WHERE TABLE_SCHEMA IN ('mysql', 'information_schema', 'performance_schema', 'sys')
            ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION
            """;
        using var reader = command.ExecuteReader();
        var types = new List<DatabaseCatalogType>();
        while (reader.Read())
        {
            types.Add(new DatabaseCatalogType(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                reader.GetBoolean(5), reader.GetBoolean(6), null, null, reader.GetString(9)));
        }

        return new DatabaseTypeCatalogSnapshot(
            "MySQL information_schema.COLUMNS over system schemas",
            "Distinct native type families observed in system catalog columns; not a complete list of types accepted by the server.",
            types);
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string? ReadOptionalComment(DbConnection connection, string sql, params (string Name, string Value)[] values)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in values) AddParameter(command, $"@{name}", value);
        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string QuoteTestTable(string name)
    {
        if (name.Length == 0 || name.Any(character => !(char.IsAsciiLetterOrDigit(character) || character == '_')))
            throw new ArgumentException("Native type test table names must contain only ASCII letters, digits, and underscores.", nameof(name));
        return $"`{name}`";
    }
}
