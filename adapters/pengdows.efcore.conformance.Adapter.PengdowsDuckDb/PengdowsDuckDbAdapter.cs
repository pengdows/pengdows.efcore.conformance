using System.Data.Common;
using System.Text.RegularExpressions;
using DuckDB.NET.Data;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.duckdb;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.PengdowsDuckDb;

/// <summary>Candidate adapter for the pengdows ADO.NET DuckDB EF provider.</summary>
public sealed class PengdowsDuckDbAdapter : IAdoNetProviderAdapter, INativeTypeMappingProfile, INativeTypeTableCleanupAdapter, IParameterizedCollectionModeAdapter, ICollationTestAdapter, ISchemaCommentMetadataAdapter, IIndexMetadataAdapter
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
            "relational", "transactions", "migrations", "raw-sql", "execute-update",
            "execute-delete", "external-connection", "parameterized-collection-modes", "native-type-fidelity",
            "case-insensitive-collation", "schema-comments"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "isolation-serializable", "ambient-transactions", "foreign-key-cascade", "savepoints", "filtered-indexes", "json-mapping", "primitive-collections",
            "least-greatest-functions", "migration-rename-index", "migration-check-constraint",
            "index-schema-metadata", "migration-add-constraint-operations",
            "migration-set-default", "index-sort-metadata", "sequences",
            "schemas", "database-collation-migration", "schema-qualified-functions"
        });

    public string Name => "pengdows.efcore.adonet DuckDB";

    public string Version => typeof(DuckDbDbContextOptionsBuilderExtensions).Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

    public IReadOnlyList<NativeTypeMapping> NativeTypeMappings => DeclaredNativeTypes;

    public string CaseInsensitiveCollation => "NOCASE";

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

    public async Task CleanupNativeTypeTablesAsync(string connectionString, IReadOnlyList<string> tableNames, CancellationToken cancellationToken)
    {
        await using var connection = new DuckDBConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = string.Join(";", tableNames.Select(name => $"DROP TABLE IF EXISTS \"{name}\""));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public DbConnection CreateConnection(string connectionString) => new DuckDBConnection(connectionString);

    public IReadOnlyList<IndexColumnMetadata> GetIndexColumns(DbConnection openConnection, string tableName, string indexName)
    {
        using var command = openConnection.CreateCommand();
        command.CommandText = "SELECT sql FROM duckdb_indexes() WHERE lower(table_name) = lower($tableName) AND lower(index_name) = lower($indexName)";
        AddParameter(command, "tableName", tableName);
        AddParameter(command, "indexName", indexName);
        using var reader = command.ExecuteReader();
        if (reader.Read() && !reader.IsDBNull(0)) return ParseIndexDefinition(reader.GetString(0));

        reader.Close();
        command.CommandText = "SELECT constraint_column_names FROM duckdb_constraints() WHERE lower(table_name) = lower($tableName) AND constraint_type = 'UNIQUE' AND lower(constraint_name) = lower($indexName)";
        using var constraintReader = command.ExecuteReader();
        if (!constraintReader.Read() || constraintReader.IsDBNull(0)) return [];
        if (constraintReader.GetValue(0) is not IEnumerable<string> names) return [];
        return names.Select((name, ordinal) => new IndexColumnMetadata(name, ordinal, false)).ToArray();
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static IReadOnlyList<IndexColumnMetadata> ParseIndexDefinition(string sql)
    {
        var match = Regex.Match(sql, @"\((?<columns>[^()]*(?:\([^()]*\)[^()]*)*)\)", RegexOptions.CultureInvariant);
        if (!match.Success) return [];
        return match.Groups["columns"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((expression, ordinal) =>
            {
                var descending = Regex.IsMatch(expression, @"\s+DESC\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                var column = Regex.Replace(expression, @"\s+(ASC|DESC)\s*$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim('"', '`', '[', ']');
                return new IndexColumnMetadata(column, ordinal, descending);
            }).ToArray();
    }

    public string? GetTableComment(DbConnection openConnection, string tableName) =>
        ReadComment(openConnection, "SELECT comment FROM duckdb_tables() WHERE lower(table_name) = lower($tableName)", tableName, null);

    public string? GetColumnComment(DbConnection openConnection, string tableName, string columnName) =>
        ReadComment(openConnection, "SELECT comment FROM duckdb_columns() WHERE lower(table_name) = lower($tableName) AND lower(column_name) = lower($columnName)", tableName, columnName);

    private static string? ReadComment(DbConnection connection, string sql, string tableName, string? columnName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "tableName";
        tableParameter.Value = tableName;
        command.Parameters.Add(tableParameter);
        if (columnName is not null)
        {
            var columnParameter = command.CreateParameter();
            columnParameter.ParameterName = "columnName";
            columnParameter.Value = columnName;
            command.Parameters.Add(columnParameter);
        }

        var result = command.ExecuteScalar();
        return result is null or DBNull ? null : Convert.ToString(result, System.Globalization.CultureInfo.InvariantCulture);
    }
}
