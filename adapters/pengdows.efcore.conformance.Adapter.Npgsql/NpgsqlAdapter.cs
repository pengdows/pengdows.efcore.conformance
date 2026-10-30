using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.Npgsql;

/// <summary>EF Core 10 adapter for PostgreSQL using Npgsql.</summary>
public sealed class NpgsqlAdapter : IAdoNetProviderAdapter, INativeTypeMappingProfile, IParameterizedCollectionModeAdapter, IIndexMetadataAdapter, ICollationTestAdapter, ISchemaCommentMetadataAdapter
{
    private static readonly IReadOnlyList<NativeTypeMapping> DeclaredNativeTypes =
    [
        new("boolean", typeof(bool)),
        new("smallint", typeof(short)),
        new("integer", typeof(int)),
        new("bigint", typeof(long)),
        new("numeric", typeof(decimal)),
        new("money", typeof(decimal), "money"),
        new("real", typeof(float)),
        new("double precision", typeof(double)),
        new("character varying", typeof(string)),
        new("character", typeof(string)),
        new("text", typeof(string)),
        new("bytea", typeof(byte[])),
        new("date", typeof(DateOnly)),
        new("time without time zone", typeof(TimeOnly)),
        new("timestamp without time zone", typeof(DateTime)),
        new("timestamp with time zone", typeof(DateTime), "timestamp with time zone"),
        new("timestamp with time zone", typeof(DateTimeOffset)),
        new("interval", typeof(TimeSpan)),
        new("uuid", typeof(Guid)),
        new("inet", typeof(System.Net.IPAddress)),
        new("cidr", typeof(System.Net.IPNetwork)),
        new("macaddr", typeof(System.Net.NetworkInformation.PhysicalAddress)),
        new("macaddr8", typeof(System.Net.NetworkInformation.PhysicalAddress)),
        new("int4range", typeof(NpgsqlTypes.NpgsqlRange<int>)),
        new("int8range", typeof(NpgsqlTypes.NpgsqlRange<long>)),
        new("numrange", typeof(NpgsqlTypes.NpgsqlRange<decimal>)),
        new("daterange", typeof(NpgsqlTypes.NpgsqlRange<DateOnly>)),
        new("tsrange", typeof(NpgsqlTypes.NpgsqlRange<DateTime>)),
        new("tstzrange", typeof(NpgsqlTypes.NpgsqlRange<DateTime>)),
        new("int4multirange", typeof(NpgsqlTypes.NpgsqlRange<int>[])),
        new("int8multirange", typeof(NpgsqlTypes.NpgsqlRange<long>[])),
        new("nummultirange", typeof(NpgsqlTypes.NpgsqlRange<decimal>[])),
        new("datemultirange", typeof(NpgsqlTypes.NpgsqlRange<DateOnly>[])),
        new("tsmultirange", typeof(NpgsqlTypes.NpgsqlRange<DateTime>[])),
        new("tstzmultirange", typeof(NpgsqlTypes.NpgsqlRange<DateTime>[])),
        new("boolean[]", typeof(bool[])),
        new("smallint[]", typeof(short[])),
        new("bigint[]", typeof(long[])),
        new("real[]", typeof(float[])),
        new("double precision[]", typeof(double[])),
        new("numeric[]", typeof(decimal[])),
        new("text[]", typeof(string[])),
        new("character varying[]", typeof(string[])),
        new("character[]", typeof(string[])),
        new("date[]", typeof(DateOnly[])),
        new("time without time zone[]", typeof(TimeOnly[])),
        new("timestamp without time zone[]", typeof(DateTime[])),
        new("timestamp with time zone[]", typeof(DateTime[])),
        new("time with time zone[]", typeof(DateTimeOffset[])),
        new("interval[]", typeof(TimeSpan[])),
        new("uuid[]", typeof(Guid[])),
        new("inet[]", typeof(System.Net.IPAddress[])),
        new("cidr[]", typeof(System.Net.IPNetwork[])),
        new("macaddr[]", typeof(System.Net.NetworkInformation.PhysicalAddress[])),
        new("macaddr8[]", typeof(System.Net.NetworkInformation.PhysicalAddress[])),
        new("json", typeof(string)),
        new("jsonb", typeof(string)),
        new("xml", typeof(string), "xml"),
        new("integer[]", typeof(int[]))
    ];

    private static readonly ProviderCapabilities DeclaredCapabilities = new(
        new HashSet<string>(StringComparer.Ordinal)
        {
            "relational", "transactions", "ambient-transactions", "savepoints", "migrations",
            "filtered-indexes", "json-mapping", "raw-sql", "execute-update", "execute-delete",
            "primitive-collections", "external-connection", "parameterized-collection-modes",
            "least-greatest-functions", "migration-rename-index", "migration-check-constraint",
            "index-schema-metadata", "migration-add-constraint-operations", "migration-set-default",
            "index-sort-metadata", "sequences", "schemas", "schema-qualified-functions", "case-insensitive-collation", "schema-comments", "native-type-fidelity"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "database-collation-migration"
        });

    public string Name => "Npgsql EF Core PostgreSQL";

    public string CaseInsensitiveCollation => "ef10conformance_ci";

    public string Version => typeof(NpgsqlConnection).Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

    public IReadOnlyList<NativeTypeMapping> NativeTypeMappings => DeclaredNativeTypes;

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
