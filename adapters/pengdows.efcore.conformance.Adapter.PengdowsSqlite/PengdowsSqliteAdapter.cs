using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.adonet.sqlite;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.PengdowsSqlite;

/// <summary>Sample adapter for exercising the suite against the pengdows ADO.NET SQLite provider.</summary>
public sealed class PengdowsSqliteAdapter : IAdoNetProviderAdapter
{
    private static readonly ProviderCapabilities SupportedCapabilities = new(
        new HashSet<string>(StringComparer.Ordinal)
        {
            "relational",
            "transactions",
            "savepoints",
            "migrations",
            "raw-sql",
            "execute-update",
            "execute-delete"
        },
        new HashSet<string>(StringComparer.Ordinal));

    public string Name => "pengdows.efcore.adonet SQLite";

    public string Version => typeof(SqliteDbContextOptionsBuilderExtensions).Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => SupportedCapabilities;

    public void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseAdoNetSqlite(connectionString);

    public Task InitializeDatabaseAsync(DbContext context, CancellationToken cancellationToken) =>
        context.Database.EnsureCreatedAsync(cancellationToken);

    public Task CleanupDatabaseAsync(DbContext context, CancellationToken cancellationToken) =>
        context.Database.EnsureDeletedAsync(cancellationToken);

    public DbConnection CreateConnection(string connectionString) => new SqliteConnection(connectionString);
}
