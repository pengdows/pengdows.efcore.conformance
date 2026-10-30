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

        var adapter = ProviderTestRuntime.SchemaCommentMetadataAdapter;
        Assert.Equal("EF10 conformance table comment", adapter.GetTableComment(connection, "ef10conformance_commentedrecord"));
        Assert.Equal("EF10 conformance column comment", adapter.GetColumnComment(connection, "ef10conformance_commentedrecord", "value"));
    }
}
