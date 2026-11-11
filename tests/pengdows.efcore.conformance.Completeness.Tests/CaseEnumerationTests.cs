using pengdows.efcore.conformance;

namespace pengdows.efcore.conformance.Completeness.Tests;

/// <summary>
///     Phase 0 of the black-box harness: each callable public member becomes a case, tagged with the driver that can call it and with
///     its sync/async pair, so a case is never counted for a member it did not call.
/// </summary>
public class CaseEnumerationTests
{
    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.Metadata.IReadOnlyEntityType", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.Query.SqlExpressions.SqlExpression", "query")]
    [InlineData("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions", "query")]
    [InlineData("Microsoft.EntityFrameworkCore.RelationalQueryableExtensions", "query")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade", "database")]
    [InlineData("Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions", "database")]
    [InlineData("Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry", "tracker")]
    [InlineData("Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder", "migrations")]
    [InlineData("Microsoft.EntityFrameworkCore.Design.ReverseEngineering.ScaffoldedModel", "scaffolding")]
    [InlineData("Microsoft.EntityFrameworkCore.DbContext", "context")]
    public void A_declaring_type_maps_to_the_driver_that_can_call_it(string declaringType, string driver)
    {
        Assert.Equal(driver, CaseEnumeration.DriverFor(declaringType));
    }

    [Fact]
    public void An_async_member_pairs_with_its_sync_name()
    {
        Assert.True(CaseEnumeration.IsAsync("SaveChangesAsync"));
        Assert.Equal("SaveChanges", CaseEnumeration.SyncNameOf("SaveChangesAsync"));
        Assert.False(CaseEnumeration.IsAsync("SaveChanges"));
        Assert.Equal("SaveChanges", CaseEnumeration.SyncNameOf("SaveChanges"));
    }

    [Fact]
    public void A_member_with_no_known_driver_is_reported_as_unassigned_not_guessed()
    {
        Assert.Equal("unassigned", CaseEnumeration.DriverFor("Microsoft.EntityFrameworkCore.Something.Unknown"));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId", "diagnostics")]
    [InlineData("Microsoft.EntityFrameworkCore.Diagnostics.RelationalLoggerExtensions", "diagnostics")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.ValueConversion.ValueConverter", "conversion")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.Json.JsonTypeMapping", "json")]
    [InlineData("Microsoft.EntityFrameworkCore.Update.ColumnModification", "context")]
    [InlineData("Microsoft.EntityFrameworkCore.RelationalPropertyExtensions", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.ExecutionStrategyExtensions", "context")]
    public void Members_of_the_remaining_namespaces_map_to_their_driver(string declaringType, string driver)
    {
        Assert.Equal(driver, CaseEnumeration.DriverFor(declaringType));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Diagnostics.CoreStrings")]
    [InlineData("Microsoft.EntityFrameworkCore.Internal.DesignStrings")]
    public void Resource_string_classes_are_excluded_with_a_reason(string declaringType)
    {
        var reason = CaseEnumeration.ExclusionFor(declaringType);

        Assert.NotNull(reason);
        Assert.Contains("resource", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_driven_member_has_no_exclusion()
    {
        Assert.Null(CaseEnumeration.ExclusionFor("Microsoft.EntityFrameworkCore.DbContext"));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.DbSet`1", "context")]
    [InlineData("Microsoft.EntityFrameworkCore.ModelBuilder", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.RelationalConnection", "database")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.RelationalTransaction", "database")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.RelationalTypeMapping", "conversion")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.CoreOptionsExtension", "configuration")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.RelationalDbContextOptionsBuilder`2", "configuration")]
    [InlineData("Microsoft.EntityFrameworkCore.EF", "query")]
    [InlineData("Microsoft.EntityFrameworkCore.ScaffoldingModelExtensions", "scaffolding")]
    public void Public_surface_types_map_to_a_driver(string declaringType, string driver)
    {
        Assert.Equal(driver, CaseEnumeration.DriverFor(declaringType));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.Internal.RelationalDatabaseFacadeDependencies")]
    [InlineData("Microsoft.EntityFrameworkCore.Internal.DbContextDependencies")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.RelationalCommandParameterObject")]
    public void Service_dependency_bags_are_excluded_with_a_reason(string declaringType)
    {
        Assert.Contains("dependency", CaseEnumeration.ExclusionFor(declaringType) ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.IndentedStringBuilder")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.Uniquifier")]
    public void EF_utility_types_are_excluded_with_a_reason(string declaringType)
    {
        Assert.Contains("utility", CaseEnumeration.ExclusionFor(declaringType) ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.EntityState", "enum")]
    [InlineData("Microsoft.EntityFrameworkCore.PropertyAccessMode", "enum")]
    [InlineData("Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException", "context")]
    [InlineData("Microsoft.EntityFrameworkCore.DbUpdateException", "context")]
    [InlineData("Microsoft.EntityFrameworkCore.IndexAttribute", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.PrecisionAttribute", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.ExecutionStrategy", "context")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.ValueBuffer", "database")]
    public void Remaining_public_types_map_to_a_driver(string declaringType, string driver)
    {
        Assert.Equal(driver, CaseEnumeration.DriverFor(declaringType));
    }

    [Fact]
    public void Enum_and_attribute_declarations_are_driven_not_excluded()
    {
        Assert.Null(CaseEnumeration.ExclusionFor("Microsoft.EntityFrameworkCore.EntityState"));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Internal.DbContextLease", "context")]
    [InlineData("Microsoft.EntityFrameworkCore.Internal.DbContextPool`1", "context")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.AnnotatableBuilder`2", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.IAnnotatable", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.ModelCacheKey", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.ValueGeneration.HiLoValueGeneratorState", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.DbFunctionsExtensions", "query")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.TypeMaterializationInfo", "database")]
    [InlineData("Microsoft.EntityFrameworkCore.DbLoggerCategory", "diagnostics")]
    [InlineData("Microsoft.EntityFrameworkCore.ChangeTrackingStrategy", "enum")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.DbContextOptionsExtensionInfo", "configuration")]
    public void Second_wave_public_types_map_to_a_driver(string declaringType, string driver)
    {
        Assert.Equal(driver, CaseEnumeration.DriverFor(declaringType));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Internal.ExpressionExtensions")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.Internal.LazyLoader")]
    public void Internal_namespace_types_are_excluded_with_a_reason(string declaringType)
    {
        Assert.Contains("internal", CaseEnumeration.UnassignedExclusionFor(declaringType) ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.IRelationalCommandBuilder")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.ITypeMappingSource")]
    public void Provider_service_interfaces_are_excluded_with_a_reason(string declaringType)
    {
        Assert.Contains("provider service", CaseEnumeration.UnassignedExclusionFor(declaringType) ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.BoolTypeMapping", "conversion")]
    [InlineData("Microsoft.EntityFrameworkCore.ValueGeneration.GuidValueGenerator", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.ValueGeneration.HiLoValueGenerator`1", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.DbLoggerCategory+Query", "diagnostics")]
    [InlineData("Microsoft.EntityFrameworkCore.SaveChangesFailedEventArgs", "context")]
    [InlineData("Microsoft.EntityFrameworkCore.EFExtensions", "query")]
    [InlineData("Microsoft.EntityFrameworkCore.ChangeTrackerExtensions", "tracker")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.DebugView", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.ProductInfo", "configuration")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.StoreTypePostfix", "enum")]
    [InlineData("System.Transactions.TransactionsDatabaseFacadeExtensions", "database")]
    public void Third_wave_public_types_map_to_a_driver(string declaringType, string driver)
    {
        Assert.Equal(driver, CaseEnumeration.DriverFor(declaringType));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.EntityFrameworkServicesBuilder")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.TypeMappingSource")]
    [InlineData("Microsoft.EntityFrameworkCore.Storage.Database")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.ModelValidator")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.ConcurrencyDetectorCriticalSectionDisposer")]
    public void Third_wave_provider_pipeline_types_are_excluded_with_a_reason(string declaringType)
    {
        Assert.Contains("provider", CaseEnumeration.UnassignedExclusionFor(declaringType) ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.RelationalPropertyExtensions", "model")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.ModelCacheKeyFactory", "provider service")]
    [InlineData("Microsoft.EntityFrameworkCore.Infrastructure.ServiceProviderAccessor", "provider service")]
    [InlineData("Microsoft.EntityFrameworkCore.EFExtensions+<G>$E875929AFAAB9F9D41CDDE4E1FF47548", "compiler-generated")]
    public void Last_types_are_driven_or_excluded_with_a_reason(string declaringType, string expected)
    {
        var driver = CaseEnumeration.DriverFor(declaringType);
        var reason = CaseEnumeration.UnassignedExclusionFor(declaringType) ?? CaseEnumeration.ExclusionFor(declaringType) ?? "";
        Assert.True(driver == expected || reason.Contains(expected, StringComparison.OrdinalIgnoreCase), $"{declaringType}: driver {driver}, reason {reason}");
    }

    [Theory]
    [InlineData("Mutable model metadata APIs define EF Core model construction behavior.")]
    [InlineData("Read-only model metadata APIs are owned by EF Core and are independent of relational provider execution behavior.")]
    [InlineData("Change tracking entry and collection APIs are owned by EF Core.")]
    [InlineData("Model convention execution and convention set construction are owned by EF Core.")]
    public void External_surface_exclusion_reasons_are_brought_into_scope(string reason)
    {
        Assert.True(CaseEnumeration.IsExternalSurface(reason));
    }

    [Fact]
    public void Internal_exclusion_reasons_stay_excluded()
    {
        Assert.False(CaseEnumeration.IsExternalSurface("EF internal type: not public behaviour"));
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.Query.SqlExpressions.SelectExpression")]
    [InlineData("Microsoft.EntityFrameworkCore.Query.QueryableMethods")]
    [InlineData("Microsoft.EntityFrameworkCore.Query.Internal.BufferedDataReader")]
    [InlineData("Microsoft.EntityFrameworkCore.Query.SqlExpressions.SqlExpressionFactory")]
    public void Query_pipeline_internals_are_excluded_not_driven(string declaringType)
    {
        Assert.NotNull(CaseEnumeration.ExclusionFor(declaringType));
        Assert.Contains("query pipeline", CaseEnumeration.ExclusionFor(declaringType) ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions")]
    [InlineData("Microsoft.EntityFrameworkCore.RelationalQueryableExtensions")]
    [InlineData("Microsoft.EntityFrameworkCore.EF")]
    public void Public_query_surface_is_driven(string declaringType)
    {
        Assert.Null(CaseEnumeration.ExclusionFor(declaringType));
        Assert.Equal("query", CaseEnumeration.DriverFor(declaringType));
    }
}
