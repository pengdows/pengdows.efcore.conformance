using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.MicrosoftSqlite;

/// <summary>Reference adapter using Microsoft's EF Core SQLite provider.</summary>
public sealed class MicrosoftSqliteAdapter : IAdoNetProviderAdapter, IParameterizedCollectionModeAdapter
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
            "external-connection",
            "parameterized-collection-modes"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "ambient-transactions",
            "least-greatest-functions"
        });

    public string Name => "Microsoft EF Core SQLite";

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
}
