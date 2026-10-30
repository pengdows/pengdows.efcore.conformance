using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.PomeloMySql;

/// <summary>Optional EF Core 10 adapter for MySQL-compatible servers using Pomelo.</summary>
public sealed class PomeloMySqlAdapter : IAdoNetProviderAdapter, INativeTypeMappingProfile, IParameterizedCollectionModeAdapter, ICollationTestAdapter, IIndexMetadataAdapter, ISchemaCommentMetadataAdapter
{
    private static readonly IReadOnlyList<NativeTypeMapping> DeclaredNativeTypes =
    [
        new("BOOL", typeof(bool), "tinyint"),
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
        new("DECIMAL", typeof(decimal)),
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
        new("TIME", typeof(TimeSpan)),
        new("TIMESTAMP", typeof(DateTime)),
        new("YEAR", typeof(int))
    ];

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
            "database-collation-migration",
            "raw-sql",
            "execute-update",
            "execute-delete",
            "external-connection",
            "parameterized-collection-modes",
            "native-type-fidelity"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "filtered-indexes",
            "json-mapping",
            "primitive-collections",
            "least-greatest-functions",
            "migration-set-default",
            "sequences",
            "schemas",
            "schema-qualified-functions"
        });

    public string Name => "Pomelo EF Core MySQL";

    public string CaseInsensitiveCollation => "utf8mb4_0900_ai_ci";

    public string Version => typeof(Microsoft.EntityFrameworkCore.MySqlDbContextOptionsBuilderExtensions)
        .Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

    public IReadOnlyList<NativeTypeMapping> NativeTypeMappings => DeclaredNativeTypes;

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

    public string? GetTableComment(DbConnection openConnection, string tableName) => ReadOptionalComment(
        openConnection,
        "SELECT TABLE_COMMENT FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName",
        ("tableName", tableName));

    public string? GetColumnComment(DbConnection openConnection, string tableName, string columnName) => ReadOptionalComment(
        openConnection,
        "SELECT COLUMN_COMMENT FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tableName AND COLUMN_NAME = @columnName",
        ("tableName", tableName),
        ("columnName", columnName));

    private static string? ReadOptionalComment(DbConnection openConnection, string sql, params (string Name, string Value)[] values)
    {
        using var command = openConnection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in values)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@{name}";
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
