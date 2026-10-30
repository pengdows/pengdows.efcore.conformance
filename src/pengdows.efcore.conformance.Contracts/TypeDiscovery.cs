using System.Data;
using System.Data.Common;
using System.Runtime.Loader;
using System.Text.Json.Serialization;

namespace pengdows.efcore.conformance;

public sealed record DatabaseTypeDefinition(
    string DatabaseTypeName,
    string? ClrTypeName,
    string? ProviderTypeId,
    long? MaximumLength,
    int? MaximumPrecision,
    int? MaximumScale,
    bool? IsNullable,
    bool? IsFixedLength,
    bool? IsUnsigned,
    string DiscoverySource,
    IReadOnlyDictionary<string, object?> RawMetadata)
{
    /// <summary>Exact CLR type identity when the provider returned a System.Type value.</summary>
    public string? ClrTypeAssemblyQualifiedName { get; init; }

    /// <summary>True when discovered independently from the EF model's currently declared columns.</summary>
    public bool IsIndependentDiscovery { get; init; }

    public string DiscoveryClassification => IsIndependentDiscovery ? "INDEPENDENT" : "SELF_DERIVED";
}

public sealed record DatabaseCatalogType(
    string Schema,
    string Name,
    string DatabaseTypeName,
    string Kind,
    string Category,
    bool IsArray,
    bool IsDefined,
    string? ElementTypeName,
    string? BaseTypeName,
    string? RelationName);

public sealed record DatabaseTypeCatalogSnapshot(
    string Source,
    string Scope,
    IReadOnlyList<DatabaseCatalogType> Types,
    string? ScalarComparisonSchema = null);

public sealed record DatabaseTypeCatalogComparison(
    string Source,
    string Scope,
    int CatalogTypeCount,
    IReadOnlyList<string> CatalogTypesMissingFromAdoNet,
    IReadOnlyList<string> AdoNetTypesMissingFromCatalog,
    IReadOnlyList<DatabaseCatalogType> Types)
{
    public int ExactNameMatchCount { get; init; }

    public IReadOnlyList<DatabaseTypeNameMatch> NameMatches { get; init; } = Array.Empty<DatabaseTypeNameMatch>();

    public IReadOnlyList<string> ExactNameComparisonLimitations { get; init; } = Array.Empty<string>();
}

public sealed record DatabaseTypeNameMatch(string CatalogTypeName, string AdoNetTypeName, string MatchKind);

public static class DatabaseTypeCatalogComparer
{
    public static DatabaseTypeCatalogComparison Compare(
        DatabaseTypeCatalogSnapshot catalog,
        IReadOnlyList<DatabaseTypeDefinition> adoNetTypes)
    {
        var comparisonTypes = string.IsNullOrWhiteSpace(catalog.ScalarComparisonSchema)
            ? catalog.Types
            : catalog.Types.Where(type => string.Equals(
                type.Schema, catalog.ScalarComparisonSchema, StringComparison.OrdinalIgnoreCase));
        var catalogNames = comparisonTypes
            .Select(type => type.DatabaseTypeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var adoNetNames = adoNetTypes.Select(type => type.DatabaseTypeName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var exactMatches = catalogNames.Intersect(adoNetNames, StringComparer.OrdinalIgnoreCase)
            .Select(name => new DatabaseTypeNameMatch(name, name, "EXACT"))
            .ToList();
        var unmatchedCatalogNames = catalogNames.Except(adoNetNames, StringComparer.OrdinalIgnoreCase).ToArray();
        var unmatchedAdoNetNames = adoNetNames.Except(catalogNames, StringComparer.OrdinalIgnoreCase).ToArray();
        var catalogAliases = comparisonTypes
            .GroupBy(type => NormalizeName(type.DatabaseTypeName), StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Select(type =>
                (Name: type.DatabaseTypeName, Normalized: NormalizeName(type.DatabaseTypeName), IsArray: type.IsArray)))
            .GroupBy(item => item.Normalized, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Name).ToArray(), StringComparer.OrdinalIgnoreCase);
        var adoNetAliases = adoNetNames
            .Select(name => (Name: name, Normalized: NormalizeName(name)))
            .GroupBy(item => item.Normalized, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Name).ToArray(), StringComparer.OrdinalIgnoreCase);

        foreach (var (normalized, catalogAliasNames) in catalogAliases)
        {
            if (adoNetAliases.TryGetValue(normalized, out var adoNetAliasNames) && adoNetAliasNames.Length == 1)
            {
                var catalogName = catalogAliasNames.FirstOrDefault(name => unmatchedCatalogNames.Contains(name, StringComparer.OrdinalIgnoreCase));
                var adoNetName = adoNetAliasNames[0];
                if (catalogName is not null && !string.Equals(catalogName, adoNetName, StringComparison.OrdinalIgnoreCase))
                {
                    exactMatches.Add(new DatabaseTypeNameMatch(catalogName, adoNetName, "NORMALIZED_ALIAS"));
                }
            }
        }

        var matchedCatalogNames = exactMatches.Select(match => match.CatalogTypeName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchedAdoNetNames = exactMatches.Where(match => match.MatchKind != "EXACT").Select(match => match.AdoNetTypeName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new DatabaseTypeCatalogComparison(
            catalog.Source,
            catalog.Scope,
            catalogNames.Length,
            unmatchedCatalogNames.Where(name => !matchedCatalogNames.Contains(name))
                .Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            unmatchedAdoNetNames.Where(name => !matchedAdoNetNames.Contains(name))
                .Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            catalog.Types)
        {
            ExactNameMatchCount = exactMatches.Count(match => match.MatchKind == "EXACT"),
            NameMatches = exactMatches.OrderBy(match => match.CatalogTypeName, StringComparer.OrdinalIgnoreCase).ToArray(),
            ExactNameComparisonLimitations =
            [
                "Name comparison is exact and case-insensitive; schema qualification, aliases, array syntax, and provider-specific type spellings can appear as differences.",
                "The database catalog inventory includes pseudo-types, composites, domains, arrays, extension types, and system types; missing ADO.NET metadata is not automatically an EF mapping defect."
            ]
        };
    }

    private static string NormalizeName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();
        var arraySuffix = string.Empty;
        while (normalized.EndsWith("[]", StringComparison.Ordinal))
        {
            normalized = normalized[..^2];
            arraySuffix += "[]";
        }

        if (normalized.StartsWith("pg_catalog.", StringComparison.Ordinal))
        {
            normalized = normalized["pg_catalog.".Length..];
        }

        normalized = normalized switch
        {
            "bool" => "boolean",
            "int2" => "smallint",
            "int4" => "integer",
            "int8" => "bigint",
            "float4" => "real",
            "float8" => "double precision",
            "varchar" => "character varying",
            "bpchar" => "character",
            "varbit" => "bit varying",
            "timestamp" => "timestamp without time zone",
            "timestamptz" => "timestamp with time zone",
            "time" => "time without time zone",
            "timetz" => "time with time zone",
            _ => normalized
        };

        if (normalized.StartsWith('_'))
        {
            normalized = NormalizeName(normalized[1..]) + "[]";
        }

        return normalized + arraySuffix;
    }
}

public sealed record TypeDiscoveryReport(
    string AdoNetAssemblyName,
    IReadOnlyList<string> MetadataCollections,
    IReadOnlyList<DatabaseTypeDefinition> Types,
    IReadOnlyList<DatabaseTypeDefinition> SupplementalTypes,
    IReadOnlyList<string> Diagnostics)
{
    public IReadOnlyList<NativeTypeCoverageResult> NativeTypeCoverage { get; init; } = Array.Empty<NativeTypeCoverageResult>();

    public DatabaseTypeCatalogComparison? CatalogComparison { get; init; }

    public bool HasIndependentTypeInventory => Types.Count > 0 || SupplementalTypes.Any(type => type.IsIndependentDiscovery);

    public bool UsesOnlySelfDerivedTypeEvidence => !HasIndependentTypeInventory && SupplementalTypes.Count > 0;

    public int SelfDerivedSupplementalTypeCount => SupplementalTypes.Count(type => !type.IsIndependentDiscovery);

    public bool DataTypesCollectionAdvertised => MetadataCollections.Contains("DataTypes", StringComparer.OrdinalIgnoreCase);

    public int TypesWithExactTestLinks => NativeTypeCoverage.Count(item => item.Outcome == "LINKED");

    public int TypesWithFamilyTestLinks => NativeTypeCoverage.Count(item => item.Outcome == "FAMILY_LINKED");

    public int TypesWithoutDeclaredTestLinks => NativeTypeCoverage.Count(item => item.Outcome == "UNLINKED");

    public int TypesWithExecutedRoundTripPass => NativeTypeCoverage.Count(item => item.Outcome == "PASS");

    public int TypesWithExecutedRoundTripFailure => NativeTypeCoverage.Count(item => item.Outcome == "FAIL");

    public int TypesWithoutMappingProfile => NativeTypeCoverage.Count(item => item.Outcome == "UNLINKED");

    [JsonIgnore]
    [Obsolete("Use TypesWithExactTestLinks; a declared link does not mean the native type was executed.")]
    public int TypesWithBehavioralCoverage => TypesWithExactTestLinks;

    [JsonIgnore]
    [Obsolete("Use TypesWithFamilyTestLinks; a family link does not mean each native type was executed.")]
    public int TypesWithPartialBehavioralCoverage => TypesWithFamilyTestLinks;

    [JsonIgnore]
    [Obsolete("Use TypesWithoutDeclaredTestLinks.")]
    public int TypesWithoutBehavioralCoverage => TypesWithoutDeclaredTestLinks;

    public int DeclaredTestTypesNotObserved => NativeTypeCoverage.Count(item => item.Outcome == "NOT_OBSERVED");

    public int DuplicateTypeNameCount => Types
        .GroupBy(type => type.DatabaseTypeName, StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .Sum(group => group.Count() - 1);

    public int TypesWithoutClrMapping => Types.Concat(SupplementalTypes)
        .Count(type => string.IsNullOrWhiteSpace(type.ClrTypeName));

    public int SupplementalTypesNotAdvertisedByAdoNet => SupplementalTypes
        .Select(type => type.DatabaseTypeName)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count(typeName => !Types.Any(type => string.Equals(type.DatabaseTypeName, typeName, StringComparison.OrdinalIgnoreCase)));

    public int UnresolvedClrTypeCount => Types.Concat(SupplementalTypes).Count(type =>
        !string.IsNullOrWhiteSpace(type.ClrTypeName) && !CanResolveClrType(type));

    private static bool CanResolveClrType(DatabaseTypeDefinition definition)
    {
        if (definition.ClrTypeAssemblyQualifiedName is { Length: > 0 } assemblyQualifiedName)
        {
            if (Type.GetType(assemblyQualifiedName, throwOnError: false) is not null)
            {
                return true;
            }

            var separator = assemblyQualifiedName.IndexOf(',');
            var resolvedTypeName = separator < 0 ? assemblyQualifiedName : assemblyQualifiedName[..separator];
            return AssemblyLoadContext.All.SelectMany(context => context.Assemblies)
                .Any(assembly => assembly.GetType(resolvedTypeName, throwOnError: false, ignoreCase: false) is not null);
        }

        var fullName = definition.ClrTypeName;
        return Type.GetType(fullName!, throwOnError: false) is not null || AssemblyLoadContext.All.SelectMany(context => context.Assemblies)
            .Any(assembly => assembly.GetType(fullName!, throwOnError: false, ignoreCase: false) is not null);
    }
}

/// <summary>Reads ADO.NET's public schema metadata without assuming a fixed metadata column set.</summary>
public static class AdoNetTypeDiscovery
{
    public static TypeDiscoveryReport Discover(DbConnection connection, ISupplementalTypeDiscovery? supplementalDiscovery = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var diagnostics = new List<string>();
        var collections = new List<string>();
        var definitions = new List<DatabaseTypeDefinition>();
        IReadOnlyList<DatabaseTypeDefinition> supplementalTypes = Array.Empty<DatabaseTypeDefinition>();

        if (connection.State != ConnectionState.Open)
        {
            connection.Open();
        }

        try
        {
            var metadata = connection.GetSchema("MetaDataCollections");
            foreach (DataRow row in metadata.Rows)
            {
                var name = GetString(row, "CollectionName");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    collections.Add(name);
                }
            }
        }
        catch (Exception exception)
        {
            diagnostics.Add($"MetaDataCollections unavailable: {exception.GetType().FullName}: {exception.Message}");
        }

        if (!collections.Contains("DataTypes", StringComparer.OrdinalIgnoreCase))
        {
            diagnostics.Add("The provider did not advertise a DataTypes metadata collection.");
        }
        else
        {
            try
            {
                var dataTypes = connection.GetSchema("DataTypes");
                foreach (DataRow row in dataTypes.Rows)
                {
                    var clrTypeValue = FindRaw(row, "DataType", "ClrType");
                    var raw = row.Table.Columns
                        .Cast<DataColumn>()
                        .ToDictionary(column => column.ColumnName, column => Normalize(row[column]), StringComparer.OrdinalIgnoreCase);
                    var typeName = Find(raw, "TypeName", "DataTypeName", "Type");
                    if (string.IsNullOrWhiteSpace(typeName))
                    {
                        diagnostics.Add("A DataTypes row had no recognizable type-name column.");
                        continue;
                    }

                    definitions.Add(new DatabaseTypeDefinition(
                        typeName,
                        Find(raw, "DataType", "ClrType", "DataTypeName"),
                        Find(raw, "ProviderDbType", "ProviderType", "TypeId"),
                        FindLong(raw, "ColumnSize", "MaximumLength", "MaxLength"),
                        FindInt(raw, "MaximumPrecision", "MaxPrecision", "Precision"),
                        FindInt(raw, "MaximumScale", "MaxScale", "Scale"),
                        FindBool(raw, "IsNullable", "AllowDBNull"),
                        FindBool(raw, "IsFixedLength", "FixedLength"),
                        FindBool(raw, "IsUnsigned", "Unsigned"),
                        "ADO.NET GetSchema(DataTypes)",
                        raw)
                    {
                        IsIndependentDiscovery = true,
                        ClrTypeAssemblyQualifiedName = clrTypeValue switch
                        {
                            Type clrType => clrType.AssemblyQualifiedName,
                            string clrTypeName => Type.GetType(clrTypeName, throwOnError: false)?.AssemblyQualifiedName,
                            _ => null
                        }
                    });
                }
            }
            catch (Exception exception)
            {
                diagnostics.Add($"DataTypes unavailable: {exception.GetType().FullName}: {exception.Message}");
            }
        }

        if (supplementalDiscovery is not null)
        {
            try
            {
                supplementalTypes = supplementalDiscovery.DiscoverSupplementalTypes(connection);
            }
            catch (Exception exception)
            {
                diagnostics.Add($"Supplemental database type discovery unavailable: {exception.GetType().FullName}: {exception.Message}");
            }
        }

        if (supplementalTypes.Any(type => !type.IsIndependentDiscovery))
        {
            diagnostics.Add("Supplemental types derived from the current database schema are self-derived evidence and are excluded from independent native-type coverage counts.");
        }
        else if (definitions.Count == 0 && supplementalTypes.Count == 0)
        {
            diagnostics.Add("No independent native-type inventory is available; type coverage counts are not reported.");
        }

        return new TypeDiscoveryReport(
            connection.GetType().Assembly.GetName().Name ?? connection.GetType().FullName ?? "unknown",
            collections,
            definitions,
            supplementalTypes,
            diagnostics);
    }

    private static object? Normalize(object value) => value switch
    {
        DBNull => null,
        Type type => type.FullName,
        Enum enumValue => enumValue.ToString(),
        _ => value
    };

    private static object? FindRaw(DataRow row, params string[] names)
    {
        foreach (var name in names)
        {
            if (row.Table.Columns.Contains(name) && row[name] is not DBNull)
            {
                return row[name];
            }
        }

        return null;
    }

    private static string? GetString(DataRow row, string column) =>
        row.Table.Columns.Contains(column) ? Convert.ToString(Normalize(row[column]), System.Globalization.CultureInfo.InvariantCulture) : null;

    private static string? Find(IReadOnlyDictionary<string, object?> values, params string[] names)
    {
        foreach (var name in names)
        {
            if (values.TryGetValue(name, out var value) && value is not null)
            {
                return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    private static long? FindLong(IReadOnlyDictionary<string, object?> values, params string[] names) =>
        long.TryParse(Find(values, names), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;

    private static int? FindInt(IReadOnlyDictionary<string, object?> values, params string[] names) =>
        int.TryParse(Find(values, names), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : null;

    private static bool? FindBool(IReadOnlyDictionary<string, object?> values, params string[] names) =>
        bool.TryParse(Find(values, names), out var value) ? value : null;
}
