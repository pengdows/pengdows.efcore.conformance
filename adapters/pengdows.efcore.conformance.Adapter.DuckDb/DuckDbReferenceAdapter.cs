using System.Data.Common;
using System.Text.RegularExpressions;
using DuckDB.NET.Data;
using DuckDB.EFCore.Extensions;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.DuckDb;

/// <summary>Reference adapter for the independent DuckDB.EFCore provider.</summary>
public sealed class DuckDbReferenceAdapter : IAdoNetProviderAdapter, INativeTypeMappingProfile, INativeTypeTableCleanupAdapter, IIndexMetadataAdapter, ISupplementalTypeDiscovery, ICollationTestAdapter, ISchemaCommentMetadataAdapter
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
            "relational", "transactions", "isolation-serializable", "migrations", "raw-sql", "external-connection", "native-type-fidelity",
            "least-greatest-functions", "json-mapping", "execute-update", "execute-delete", "primitive-collections",
            "migration-set-default", "sequences", "schemas", "schema-qualified-functions", "case-insensitive-collation", "schema-comments"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "ambient-transactions", "foreign-key-cascade", "savepoints", "filtered-indexes", "parameterized-collection-modes",
            "migration-rename-index", "migration-check-constraint", "index-schema-metadata",
            "migration-add-constraint-operations",
            "index-sort-metadata", "database-collation-migration"
        });

    public string Name => "DuckDB.EFCore reference";

    public string Version => typeof(DuckDBDbContextOptionsBuilderExtensions).Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

    public IReadOnlyList<NativeTypeMapping> NativeTypeMappings => DeclaredNativeTypes;

    public string CaseInsensitiveCollation => "NOCASE";

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

    public IReadOnlyList<DatabaseTypeDefinition> DiscoverSupplementalTypes(DbConnection openConnection)
    {
        using var command = openConnection.CreateCommand();
        command.CommandText = "SELECT type_name, logical_type, type_category, type_size, type_oid FROM duckdb_types() WHERE internal ORDER BY type_name";
        using var reader = command.ExecuteReader();
        var types = new List<DatabaseTypeDefinition>();
        while (reader.Read())
        {
            var typeName = reader.GetString(0);
            var logicalType = reader.IsDBNull(1) ? null : reader.GetString(1);
            types.Add(new DatabaseTypeDefinition(
                typeName,
                ClrTypeName: null,
                ProviderTypeId: reader.IsDBNull(4) ? null : Convert.ToString(reader.GetValue(4), System.Globalization.CultureInfo.InvariantCulture),
                MaximumLength: null,
                MaximumPrecision: null,
                MaximumScale: null,
                IsNullable: null,
                IsFixedLength: null,
                IsUnsigned: null,
                DiscoverySource: "DuckDB public duckdb_types() catalog",
                RawMetadata: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["LogicalType"] = logicalType,
                    ["TypeCategory"] = reader.IsDBNull(2) ? null : reader.GetString(2),
                    ["TypeSize"] = reader.IsDBNull(3) ? null : reader.GetValue(3)
                })
            {
                IsIndependentDiscovery = true
            });
        }

        return types;
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

    public IReadOnlyList<IndexColumnMetadata> GetIndexColumns(DbConnection openConnection, string tableName, string indexName)
    {
        using var command = openConnection.CreateCommand();
        command.CommandText = "SELECT sql FROM duckdb_indexes() WHERE lower(table_name) = lower($tableName) AND lower(index_name) = lower($indexName)";
        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "tableName";
        tableParameter.Value = tableName;
        command.Parameters.Add(tableParameter);
        var indexParameter = command.CreateParameter();
        indexParameter.ParameterName = "indexName";
        indexParameter.Value = indexName;
        command.Parameters.Add(indexParameter);

        using var reader = command.ExecuteReader();
        if (reader.Read() && !reader.IsDBNull(0))
        {
            return ParseIndexDefinition(reader.GetString(0));
        }

        reader.Close();
        command.CommandText = "SELECT constraint_column_names FROM duckdb_constraints() WHERE lower(table_name) = lower($tableName) AND constraint_type = 'UNIQUE' AND lower(constraint_name) = lower($indexName)";
        using var constraintReader = command.ExecuteReader();
        if (!constraintReader.Read() || constraintReader.IsDBNull(0)) return [];
        var names = constraintReader.GetValue(0) as IEnumerable<string>;
        if (names is null) return [];
        return names.Select((name, ordinal) => new IndexColumnMetadata(name, ordinal, false)).ToArray();
    }

    private static IReadOnlyList<IndexColumnMetadata> ParseIndexDefinition(string sql)
    {
        var columnsMatch = Regex.Match(sql, @"\((?<columns>[^()]*(?:\([^()]*\)[^()]*)*)\)", RegexOptions.CultureInvariant);
        if (!columnsMatch.Success) return [];
        return columnsMatch.Groups["columns"].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((expression, ordinal) =>
            {
                var descending = Regex.IsMatch(expression, @"\s+DESC\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                var columnName = Regex.Replace(expression, @"\s+(ASC|DESC)\s*$", "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
                    .Trim('"', '`', '[', ']');
                return new IndexColumnMetadata(columnName, ordinal, descending);
            }).ToArray();
    }
}
