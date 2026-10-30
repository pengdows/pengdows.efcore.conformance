using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class IndexDirectionTests
{
    [Fact]
    [CapabilityRequirement("index-sort-metadata")]
    [BehavioralCoverage(
        "EF10-INDEX-DIRECTION-METADATA",
        "Microsoft.EntityFrameworkCore.IndexAttribute.IsDescending",
        "Microsoft.EntityFrameworkCore.IndexAttribute.AllDescending")]
    public async Task IndexAttributes_CreateIndexesWithDeclaredSortDirections()
    {
        ProviderTestRuntime.RequireCapability("index-sort-metadata");
        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        }

        await using var connection = ProviderTestRuntime.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var adapter = ProviderTestRuntime.IndexMetadataAdapter;
        var mixed = adapter.GetIndexColumns(
            connection,
            "ef10conformance_indexsort",
            "IX_EF10_IndexSort_Mixed").OrderBy(column => column.Ordinal).ToArray();
        var allDescending = adapter.GetIndexColumns(
            connection,
            "ef10conformance_indexsort",
            "IX_EF10_IndexSort_AllDescending").OrderBy(column => column.Ordinal).ToArray();

        Assert.Equal(new[] { "first", "second" }, mixed.Select(column => column.ColumnName));
        Assert.Equal(new[] { true, false }, mixed.Select(column => column.IsDescending));
        Assert.Equal(new[] { "first", "second" }, allDescending.Select(column => column.ColumnName));
        Assert.All(allDescending, column => Assert.True(column.IsDescending));
    }
}
