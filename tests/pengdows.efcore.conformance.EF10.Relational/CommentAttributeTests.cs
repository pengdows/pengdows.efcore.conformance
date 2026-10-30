using System.Data;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("schema-comments")]
public sealed class CommentAttributeTests
{
    [Fact]
    [BehavioralCoverage(
        "EF10-COMMENT-ATTRIBUTE-SCHEMA",
        "Microsoft.EntityFrameworkCore.CommentAttribute",
        "Microsoft.EntityFrameworkCore.CommentAttribute..ctor",
        "Microsoft.EntityFrameworkCore.CommentAttribute.Comment")]
    public async Task CommentAttributes_AppearInPublicDatabaseSchemaMetadata()
    {
        ProviderTestRuntime.RequireCapability("schema-comments");
        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        }

        await using var connection = ProviderTestRuntime.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        var table = connection.GetSchema("Tables");
        Assert.Contains(table.Rows.Cast<DataRow>(), row =>
            HasValue(row, "ef10conformance_commentedrecord")
            && HasValue(row, "EF10 conformance table comment"));

        var columns = connection.GetSchema("Columns");
        Assert.Contains(columns.Rows.Cast<DataRow>(), row =>
            HasValue(row, "ef10conformance_commentedrecord")
            && HasValue(row, "value")
            && HasValue(row, "EF10 conformance column comment"));
    }

    private static bool HasValue(DataRow row, string expected) => row.ItemArray.Any(value =>
        string.Equals(
            Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
            expected,
            StringComparison.OrdinalIgnoreCase));
}
