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

/// <summary>Optional provider configuration for relational query collection parameter modes.</summary>
public interface IParameterizedCollectionModeAdapter : IProviderAdapter
{
    void ConfigureParameterizedCollectionMode(
        DbContextOptionsBuilder options,
        string connectionString,
        ParameterTranslationMode mode);
}

/// <summary>Optional database-catalog discovery for types omitted from ADO.NET schema metadata.</summary>
public interface ISupplementalTypeDiscovery
{
    IReadOnlyList<DatabaseTypeDefinition> DiscoverSupplementalTypes(DbConnection openConnection);
}

/// <summary>Declares applicability only; it cannot alter test expectations or assertions.</summary>
public sealed record ProviderCapabilities(
    IReadOnlySet<string> SupportedFeatures,
    IReadOnlySet<string> UnsupportedFeatures)
{
    public static IReadOnlySet<string> KnownFeatures { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "relational",
        "transactions",
        "ambient-transactions",
        "savepoints",
        "migrations",
        "filtered-indexes",
        "json-mapping",
        "raw-sql",
        "execute-update",
        "execute-delete",
        "primitive-collections",
        "external-connection",
        "parameterized-collection-modes",
        "least-greatest-functions",
        "migration-rename-index",
        "migration-check-constraint"
    };

    public IReadOnlyList<string> UndeclaredFeatures => KnownFeatures
        .Except(SupportedFeatures, StringComparer.Ordinal)
        .Except(UnsupportedFeatures, StringComparer.Ordinal)
        .Order(StringComparer.Ordinal)
        .ToArray();

    public bool Supports(string feature) => SupportedFeatures.Contains(feature);

    public bool IsDeclared(string feature) => SupportedFeatures.Contains(feature) || UnsupportedFeatures.Contains(feature);

    public void Validate()
    {
        var conflicts = SupportedFeatures.Intersect(UnsupportedFeatures, StringComparer.Ordinal).ToArray();
        if (conflicts.Length > 0)
        {
            throw new InvalidOperationException($"Capabilities cannot be both supported and unsupported: {string.Join(", ", conflicts)}.");
        }

        var unknown = SupportedFeatures.Concat(UnsupportedFeatures).Except(KnownFeatures, StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
        {
            throw new InvalidOperationException($"Provider declared unknown capabilities: {string.Join(", ", unknown)}.");
        }
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class CapabilityRequirementAttribute(string feature) : Attribute
{
    public string Feature { get; } = feature;
}

public enum ConformanceOutcome
{
    Pass,
    Fail,
    Untranslatable,
    Unsupported,
    UndeclaredCapability,
    Error,
    Skip,
    Inconclusive
}

public enum ConformanceSeverity
{
    Hard,
    Soft
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class ConformanceSeverityAttribute(ConformanceSeverity severity) : Attribute
{
    public ConformanceSeverity Severity { get; } = severity;
}

public sealed class ConformanceInfrastructureException(string message, Exception innerException)
    : Exception(message, innerException);

public sealed class UndeclaredCapabilityException(string feature)
    : Exception($"Provider capability '{feature}' was not declared.")
{
    public string Feature { get; } = feature;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class NativeTypeCoverageAttribute(string databaseTypeName, Type clrType, string behavioralContractId) : Attribute
{
    public string DatabaseTypeName { get; } = databaseTypeName;

    public Type ClrType { get; } = clrType;

    public string BehavioralContractId { get; } = behavioralContractId;
}

public sealed record NativeTypeCoverageResult(
    string DatabaseTypeName,
    string Outcome,
    IReadOnlyList<string> ClrTypes,
    IReadOnlyList<string> BehavioralContractIds)
{
    public string? ClrTypeName { get; init; }

    public string? ProviderTypeId { get; init; }

    public string? CreateFormat { get; init; }
}

public sealed record ConformanceResult(
    string ContractId,
    string DisplayName,
    ConformanceOutcome Outcome,
    string? Reason = null,
    string? ExceptionType = null,
    ConformanceSeverity Severity = ConformanceSeverity.Hard);
