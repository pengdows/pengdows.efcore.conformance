using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Adapter.MicrosoftSqlite;

/// <summary>
/// Controlled mutant for the conformance harness: it advertises case-insensitive
/// collation support while returning SQLite's case-sensitive BINARY collation.
/// </summary>
public sealed class DefectiveCollationAdapter : IAdoNetProviderAdapter, ICollationTestAdapter, IIndexMetadataAdapter, IParameterizedCollectionModeAdapter
{
    private readonly MicrosoftSqliteAdapter _inner = new();

    public string Name => "Deliberately defective SQLite collation adapter";

    public string Version => _inner.Version;

    public ProviderCapabilities Capabilities => _inner.Capabilities;

    public string CaseInsensitiveCollation => "BINARY";

    public void Configure(DbContextOptionsBuilder options, string connectionString) =>
        _inner.Configure(options, connectionString);

    public DbConnection CreateConnection(string connectionString) => _inner.CreateConnection(connectionString);

    public IReadOnlyList<IndexColumnMetadata> GetIndexColumns(DbConnection openConnection, string tableName, string indexName) =>
        _inner.GetIndexColumns(openConnection, tableName, indexName);

    public void ConfigureParameterizedCollectionMode(DbContextOptionsBuilder options, string connectionString, ParameterTranslationMode mode) =>
        _inner.ConfigureParameterizedCollectionMode(options, connectionString, mode);

    public Task InitializeDatabaseAsync(DbContext context, CancellationToken cancellationToken) =>
        _inner.InitializeDatabaseAsync(context, cancellationToken);

    public Task CleanupDatabaseAsync(DbContext context, CancellationToken cancellationToken) =>
        _inner.CleanupDatabaseAsync(context, cancellationToken);
}
