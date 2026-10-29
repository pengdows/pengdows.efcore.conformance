using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace pengdows.efcore.conformance;

/// <summary>Configuration and database lifecycle boundary for a black-box provider run.</summary>
public interface IProviderAdapter
{
    string Name { get; }

    string Version { get; }

    void Configure(DbContextOptionsBuilder options, string connectionString);

    Task InitializeDatabaseAsync(DbContext context, CancellationToken cancellationToken);

    Task CleanupDatabaseAsync(DbContext context, CancellationToken cancellationToken);

    ProviderCapabilities Capabilities { get; }
}

/// <summary>Optional public ADO.NET connection access used only for database type discovery.</summary>
public interface IAdoNetProviderAdapter : IProviderAdapter
{
    DbConnection CreateConnection(string connectionString);
}

/// <summary>Declares applicability only; it cannot alter test expectations or assertions.</summary>
public sealed record ProviderCapabilities(
    IReadOnlySet<string> SupportedFeatures,
    IReadOnlySet<string> UnsupportedFeatures)
{
    public bool Supports(string feature) => SupportedFeatures.Contains(feature);

    public void Validate()
    {
        var conflicts = SupportedFeatures.Intersect(UnsupportedFeatures, StringComparer.Ordinal).ToArray();
        if (conflicts.Length > 0)
        {
            throw new InvalidOperationException($"Capabilities cannot be both supported and unsupported: {string.Join(", ", conflicts)}.");
        }
    }
}

public enum ConformanceOutcome
{
    Pass,
    Fail,
    Unsupported,
    Error,
    Skip
}

public sealed record ConformanceResult(
    string ContractId,
    string DisplayName,
    ConformanceOutcome Outcome,
    string? Reason = null,
    string? ExceptionType = null);
