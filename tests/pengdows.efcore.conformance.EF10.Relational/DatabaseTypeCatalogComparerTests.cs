using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

public sealed class DatabaseTypeCatalogComparerTests
{
    [Fact]
    public void Compare_MatchesExactPostgreSqlNamesAliasesAndArraysWithoutCountingAmbiguousNames()
    {
        var catalog = new DatabaseTypeCatalogSnapshot(
            "PostgreSQL catalog",
            "test scope",
            [
                CatalogType("pg_catalog", "int4", "integer"),
                CatalogType("pg_catalog", "_int4", "_int4", isArray: true),
                CatalogType("pg_catalog", "float8", "double precision"),
                CatalogType("pg_catalog", "text", "text"),
                CatalogType("pg_catalog", "varbit", "varbit"),
                CatalogType("pg_catalog", "my_domain", "my_domain", baseTypeName: "integer"),
                CatalogType("custom_schema", "int4", "integer")
            ],
            "pg_catalog");
        var adoNet = new[]
        {
            AdoType("integer"),
            AdoType("integer[]"),
            AdoType("double precision"),
            AdoType("text"),
            AdoType("bit varying")
        };

        var result = DatabaseTypeCatalogComparer.Compare(catalog, adoNet);

        Assert.Equal(6, result.CatalogTypeCount);
        Assert.Equal(3, result.ExactNameMatchCount);
        Assert.Equal(2, result.NameMatches.Count(match => match.MatchKind == "NORMALIZED_ALIAS"));
        Assert.Contains("my_domain", result.CatalogTypesMissingFromAdoNet);
        Assert.Empty(result.AdoNetTypesMissingFromCatalog);
        Assert.Contains(result.Types, type => type.Schema == "custom_schema");
    }

    [Fact]
    public void Compare_DoesNotTreatDuplicateCatalogAliasesAsOneCertainMatch()
    {
        var catalog = new DatabaseTypeCatalogSnapshot(
            "PostgreSQL catalog",
            "test scope",
            [
                CatalogType("pg_catalog", "int4", "integer"),
                CatalogType("custom_schema", "int4", "integer")
            ],
            "pg_catalog");

        var result = DatabaseTypeCatalogComparer.Compare(catalog, [AdoType("integer")]);

        Assert.Empty(result.CatalogTypesMissingFromAdoNet);
        Assert.Empty(result.AdoNetTypesMissingFromCatalog);
        Assert.Single(result.NameMatches);
    }

    private static DatabaseCatalogType CatalogType(
        string schema,
        string name,
        string databaseTypeName,
        bool isArray = false,
        string? baseTypeName = null) =>
        new(schema, name, databaseTypeName, "b", isArray ? "A" : "N", isArray, true,
            isArray ? "integer" : null, baseTypeName, null);

    private static DatabaseTypeDefinition AdoType(string name) => new(
        name, "System.Object", null, null, null, null, true, null, null,
        "test metadata", new Dictionary<string, object?>());
}
