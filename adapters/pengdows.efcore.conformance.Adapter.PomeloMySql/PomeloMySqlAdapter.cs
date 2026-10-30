using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.PomeloMySql;

/// <summary>Optional EF Core 10 adapter for MySQL-compatible servers using Pomelo.</summary>
public sealed class PomeloMySqlAdapter : IAdoNetProviderAdapter, IParameterizedCollectionModeAdapter
{
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
            "raw-sql",
            "execute-update",
            "execute-delete",
            "external-connection",
            "parameterized-collection-modes"
        },
        new HashSet<string>(StringComparer.Ordinal)
        {
            "filtered-indexes",
            "json-mapping",
            "primitive-collections",
            "least-greatest-functions"
        });

    public string Name => "Pomelo EF Core MySQL";

    public string Version => typeof(Microsoft.EntityFrameworkCore.MySqlDbContextOptionsBuilderExtensions)
        .Assembly.GetName().Version?.ToString() ?? "unknown";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

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
}
