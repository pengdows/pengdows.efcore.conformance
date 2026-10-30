using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.sqlite;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.PengdowsSqlite;

/// <summary>Sample adapter for exercising the suite against the pengdows ADO.NET SQLite provider.</summary>
public sealed class PengdowsSqliteAdapter : IAdoNetProviderAdapter, ISupplementalTypeDiscovery
{
    private static readonly ProviderCapabilities SupportedCapabilities = new(
        new HashSet<string>(StringComparer.Ordinal)
        {
            "relational",
            "transactions",
            "savepoints",
            "migrations",
            "filtered-indexes",
            "json-mapping",
            "raw-sql",
            "execute-update",
            "execute-delete",
            "primitive-collections",
            "external-connection"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "ambient-transactions"
        });

    public string Name => "pengdows.efcore.adonet SQLite";

    public string Version => typeof(pengdows.efcore.adonet.sqlite.SqliteDbContextOptionsBuilderExtensions).Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => SupportedCapabilities;

    public void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseAdoNetSqlite(connectionString);

    public Task InitializeDatabaseAsync(DbContext context, CancellationToken cancellationToken) =>
        context.Database.EnsureCreatedAsync(cancellationToken);

    public Task CleanupDatabaseAsync(DbContext context, CancellationToken cancellationToken) =>
        context.Database.EnsureDeletedAsync(cancellationToken);

    public DbConnection CreateConnection(string connectionString) => new SqliteConnection(connectionString);

    public IReadOnlyList<DatabaseTypeDefinition> DiscoverSupplementalTypes(DbConnection openConnection)
    {
        using var command = openConnection.CreateCommand();
        command.CommandText = """
            SELECT DISTINCT trim(column_info.type) AS type_name
            FROM sqlite_schema AS schema_info,
                 pragma_table_info(schema_info.name) AS column_info
            WHERE schema_info.type = 'table'
              AND column_info.type IS NOT NULL
              AND trim(column_info.type) <> ''
            ORDER BY type_name
            """;

        using var reader = command.ExecuteReader();
        var types = new List<DatabaseTypeDefinition>();
        while (reader.Read())
        {
            var typeName = reader.GetString(0);
            types.Add(new DatabaseTypeDefinition(
                typeName,
                ClrTypeName: null,
                ProviderTypeId: null,
                MaximumLength: null,
                MaximumPrecision: null,
                MaximumScale: null,
                IsNullable: null,
                IsFixedLength: null,
                IsUnsigned: null,
                DiscoverySource: "SQLite schema catalog (declared column types in existing tables)",
                RawMetadata: new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Scope"] = "Types currently used by table columns"
                }));
        }

        return types;
    }
}
