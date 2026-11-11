namespace pengdows.efcore.conformance;

/// <summary>
///     Phase 0 of the black-box harness: which driver can call a public member, and which sync member it pairs with. A member whose
///     declaring type has no known driver is "unassigned", never guessed, so it shows up as work rather than as coverage.
/// </summary>
public static class CaseEnumeration
{
    private static readonly (string Prefix, string Driver)[] Drivers =
    [
        ("Microsoft.EntityFrameworkCore.Metadata.", "model"),
        ("Microsoft.EntityFrameworkCore.RelationalEntityTypeBuilderExtensions", "model"),
        ("Microsoft.EntityFrameworkCore.Query.", "query"),
        ("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions", "query"),
        ("Microsoft.EntityFrameworkCore.RelationalQueryableExtensions", "query"),
        ("Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "database"),
        ("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "database"),
        ("Microsoft.EntityFrameworkCore.ChangeTracking.", "tracker"),
        ("Microsoft.EntityFrameworkCore.Migrations.", "migrations"),
        ("Microsoft.EntityFrameworkCore.Design.", "scaffolding"),
        ("Microsoft.EntityFrameworkCore.Scaffolding.", "scaffolding"),
        ("Microsoft.EntityFrameworkCore.DbContext", "context"),
        ("Microsoft.EntityFrameworkCore.Infrastructure.Annotatable", "model"),
        ("Microsoft.EntityFrameworkCore.Infrastructure.ConventionAnnotatable", "model"),
        ("Microsoft.EntityFrameworkCore.Diagnostics.", "diagnostics"),
        ("Microsoft.EntityFrameworkCore.Storage.ValueConversion.", "conversion"),
        ("Microsoft.EntityFrameworkCore.Storage.Json.", "json"),
        ("Microsoft.EntityFrameworkCore.Update.", "context"),
        ("Microsoft.EntityFrameworkCore.ExecutionStrategyExtensions", "context"),
        ("Microsoft.EntityFrameworkCore.Relational", "model"),
        ("Microsoft.EntityFrameworkCore.Storage.Relational", "database"),
        ("Microsoft.EntityFrameworkCore.Infrastructure.RelationalDbContextOptionsBuilder", "configuration"),
    ];

    private static readonly Dictionary<string, string> ExactDrivers = new(StringComparer.Ordinal)
    {
        ["Microsoft.EntityFrameworkCore.DbSet`1"] = "context",
        ["Microsoft.EntityFrameworkCore.Internal.InternalDbSet`1"] = "context",
        ["Microsoft.EntityFrameworkCore.Internal.EntityFinder`1"] = "context",
        ["Microsoft.EntityFrameworkCore.Internal.IEntityFinder"] = "context",
        ["Microsoft.EntityFrameworkCore.ModelBuilder"] = "model",
        ["Microsoft.EntityFrameworkCore.ModelConfigurationBuilder"] = "model",
        ["Microsoft.EntityFrameworkCore.EF"] = "query",
        ["Microsoft.EntityFrameworkCore.Storage.RelationalTypeMapping"] = "conversion",
        ["Microsoft.EntityFrameworkCore.Storage.RelationalTypeMappingInfo"] = "conversion",
        ["Microsoft.EntityFrameworkCore.Storage.TypeMappingInfo"] = "conversion",
        ["Microsoft.EntityFrameworkCore.Storage.CoreTypeMapping"] = "conversion",
        ["Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension"] = "configuration",
        ["Microsoft.EntityFrameworkCore.Infrastructure.RelationalOptionsExtension"] = "configuration",
        ["Microsoft.EntityFrameworkCore.Infrastructure.ServiceCollectionMap"] = "configuration",
        ["Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions"] = "configuration",
        ["Microsoft.EntityFrameworkCore.Storage.IRelationalConnection"] = "database",
        ["Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction"] = "database",
        ["Microsoft.EntityFrameworkCore.Storage.ISqlGenerationHelper"] = "database",
        ["Microsoft.EntityFrameworkCore.ScaffoldingModelExtensions"] = "scaffolding",
        ["Microsoft.EntityFrameworkCore.EntityState"] = "enum",
        ["Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy"] = "context",
        ["Microsoft.EntityFrameworkCore.Internal.DbContextLease"] = "context",
        ["Microsoft.EntityFrameworkCore.Internal.DbContextPool`1"] = "context",
        ["Microsoft.EntityFrameworkCore.Infrastructure.IAnnotatable"] = "model",
        ["Microsoft.EntityFrameworkCore.Infrastructure.ModelCacheKey"] = "model",
        ["Microsoft.EntityFrameworkCore.ValueGeneration.HiLoValueGeneratorState"] = "model",
        ["Microsoft.EntityFrameworkCore.DbFunctionsExtensions"] = "query",
        ["Microsoft.EntityFrameworkCore.Storage.TypeMaterializationInfo"] = "database",
        ["Microsoft.EntityFrameworkCore.Infrastructure.Annotation"] = "model",
        ["Microsoft.EntityFrameworkCore.DbLoggerCategory"] = "diagnostics",
        ["Microsoft.EntityFrameworkCore.ChangeTrackingStrategy"] = "enum",
        ["Microsoft.EntityFrameworkCore.Infrastructure.DbContextOptionsExtensionInfo"] = "configuration",
        ["Microsoft.EntityFrameworkCore.PropertyAccessMode"] = "enum",
        ["Microsoft.EntityFrameworkCore.Storage.ValueBuffer"] = "database",
    };

    private static readonly string[] UtilityTypes =
    [
        "Microsoft.EntityFrameworkCore.Infrastructure.IndentedStringBuilder",
        "Microsoft.EntityFrameworkCore.Infrastructure.Uniquifier",
        "Microsoft.EntityFrameworkCore.Infrastructure.ExpressionExtensions",
        "Microsoft.EntityFrameworkCore.Infrastructure.AnnotatableBase",
        "Microsoft.EntityFrameworkCore.Infrastructure.RuntimeAnnotatableBase",
    ];

    // Used only for members no driver claims. Internal namespaces and provider-service interfaces are not behaviour a test can call
    // directly; they are excluded with a reason, and anything else stays unassigned until a driver is chosen.
    public static string? UnassignedExclusionFor(string declaringType)
    {
        if (declaringType.Contains(".Internal.", StringComparison.Ordinal))
        {
            return "EF internal type: not public behaviour";
        }

        var simple = declaringType.Split('.').Last();
        if (declaringType.Contains("<G>$", StringComparison.Ordinal) || declaringType.Contains("<M>$", StringComparison.Ordinal))
        {
            return "compiler-generated closure: not a public type";
        }

        if (simple is "RelationalModelCustomizer" or "RelationalModelRuntimeInitializer" or "RelationalModelExtensions")
        {
            return "provider service: EF's internal wiring, not called by users";
        }

        if (simple is "TypeExtensions" or "JsonTypePlaceholder" or "TableExpressionExtensions")
        {
            return "EF internal helper: query or type plumbing, not public behaviour";
        }

        if (simple is "ModelCacheKeyFactory" or "ServiceCharacteristics" or "ServiceProviderAccessor" or "ParameterNameGeneratorFactory"
            or "MethodInfoExtensions" or "DatabaseProvider`1" or "RelationalModelValidator")
        {
            return "provider service: EF's internal wiring, not called by users";
        }

        if (simple.Contains("ServicesBuilder", StringComparison.Ordinal) || simple.EndsWith("Source", StringComparison.Ordinal)
            || simple.EndsWith("SourceBase", StringComparison.Ordinal)
            || simple is "Database" or "ModelValidator" or "ModelSource" or "ModelRuntimeInitializer" or "ModelCustomizer" or "ParameterNameGenerator"
                or "ConcurrencyDetectorCriticalSectionDisposer" or "EntityFrameworkEventSource" or "EntityFrameworkMetricsData")
        {
            return "provider service: EF's internal wiring, not called by users";
        }

        var isInterface = simple.Length > 1 && simple[0] == 'I' && char.IsUpper(simple[1]);
        return isInterface && (declaringType.Contains(".Storage.", StringComparison.Ordinal) || declaringType.Contains(".Infrastructure.", StringComparison.Ordinal))
            ? "provider service interface: implemented by providers, exercised through the public operations that call it"
            : null;
    }

    public static string DriverFor(string declaringType)
    {
        if (ExactDrivers.TryGetValue(declaringType, out var exact))
        {
            return exact;
        }

        var simple = declaringType.Split('.', '+').Last().Split('`')[0];
        if (declaringType.Contains(".Storage.Json.", StringComparison.Ordinal))
        {
            return "json";
        }

        if (simple.EndsWith("TypeMapping", StringComparison.Ordinal))
        {
            return "conversion";
        }

        if (simple.Contains("ValueGenerat", StringComparison.Ordinal))
        {
            return "model";
        }

        if (declaringType.Contains("DbLoggerCategory", StringComparison.Ordinal))
        {
            return "diagnostics";
        }

        if (simple.EndsWith("EventArgs", StringComparison.Ordinal))
        {
            return "context";
        }

        if (simple is "EFExtensions" or "DbFunctions")
        {
            return "query";
        }

        if (simple is "ChangeTrackerExtensions" or "LazyLoaderExtensions" or "ObservableCollectionExtensions")
        {
            return "tracker";
        }

        if (simple is "DbContextTransactionExtensions" or "TransactionsDatabaseFacadeExtensions")
        {
            return "database";
        }

        if (simple is "DebugView" or "MetadataDebugStringOptions" or "ModelSnapshot" or "ModelCustomizer" or "PropertyBaseExtensions" or "EntityTypeConfigurationAttribute" or "IEntityTypeConfiguration")
        {
            return "model";
        }

        if (simple is "RelationalPropertyExtensions")
        {
            return "model";
        }

        if (simple is "ProductInfo" or "AccessorExtensions")
        {
            return "configuration";
        }

        if (simple is "StoreTypePostfix" or "ParameterTranslationMode")
        {
            return "enum";
        }

        if (simple is "ReaderColumn" or "MaterializationContext" or "ExecutionResult" or "RawSqlCommand")
        {
            return "database";
        }

        if (simple is "DbSetProperty" or "PooledDbContextFactory" or "IDbContextFactory" or "NonRetryingExecutionStrategy")
        {
            return "context";
        }

        // Enums are configuration values, exercised through the members that take them; attributes configure the model; exceptions
        // are raised by the operations that throw them.
        if (declaringType.EndsWith("Attribute", StringComparison.Ordinal))
        {
            return "model";
        }

        if (declaringType.EndsWith("Exception", StringComparison.Ordinal) || declaringType.EndsWith("Behavior", StringComparison.Ordinal))
        {
            return declaringType.EndsWith("Behavior", StringComparison.Ordinal) ? "configuration" : "context";
        }

        foreach (var (prefix, driver) in Drivers)
        {
            if (declaringType.StartsWith(prefix, StringComparison.Ordinal))
            {
                return driver;
            }
        }

        return "unassigned";
    }

    // Resource-string classes hold EF's message text. A message is not behaviour a provider performs, so they are excluded, with the reason.
    public static string? ExclusionFor(string declaringType)
    {
        // The query pipeline's translators and expression types are EF internals; the public query surface is the extension classes and EF.
        if (declaringType.StartsWith("Microsoft.EntityFrameworkCore.Query.", StringComparison.Ordinal))
        {
            return "EF query pipeline internal: translator and expression types, not public surface";
        }

        if (declaringType.EndsWith("Strings", StringComparison.Ordinal))
        {
            return "EF resource-string class: message text, not provider behaviour";
        }

        if (declaringType.EndsWith("Dependencies", StringComparison.Ordinal)
            || declaringType.EndsWith("ParameterObject", StringComparison.Ordinal)
            || declaringType.EndsWith("DependencyBase", StringComparison.Ordinal))
        {
            return "service dependency bag: wiring EF passes to provider services, not provider behaviour";
        }

        return UtilityTypes.Contains(declaringType, StringComparer.Ordinal)
            ? "EF utility type: internal helper, not provider behaviour"
            : null;
    }

    // Manifest exclusions whose APIs users call directly. They are cases, not exclusions: a black-box test can call them.
    private static readonly string[] ExternalSurfaceReasons =
    [
        "Mutable model metadata APIs",
        "Read-only model metadata APIs",
        "Change tracking entry and collection APIs",
        "Model convention execution",
    ];

    public static bool IsExternalSurface(string reason)
        => ExternalSurfaceReasons.Any(prefix => reason.StartsWith(prefix, StringComparison.Ordinal));

    public static bool IsAsync(string memberName) => memberName.EndsWith("Async", StringComparison.Ordinal);

    public static string SyncNameOf(string memberName)
        => IsAsync(memberName) ? memberName[..^"Async".Length] : memberName;
}
