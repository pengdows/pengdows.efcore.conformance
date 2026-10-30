using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Trait("Category", "HarnessUnit")]
public sealed class AdoNetTypeDiscoveryTests
{
    [Fact]
    public void Discover_ReportsWhenProviderDoesNotAdvertiseDataTypes()
    {
        using var connection = new MetadataConnection(CreateCollections("Tables", "Restrictions"));

        var report = AdoNetTypeDiscovery.Discover(connection);

        Assert.False(report.DataTypesCollectionAdvertised);
        Assert.Empty(report.Types);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Contains("did not advertise a DataTypes", StringComparison.Ordinal));
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Contains("No independent native-type inventory", StringComparison.Ordinal));
    }

    [Fact]
    public void Discover_UsesAvailableColumnsAndPreservesProviderClrTypeIdentityAndRawMetadata()
    {
        var metadata = new DataTable();
        metadata.Columns.Add("TypeName", typeof(string));
        metadata.Columns.Add("DataType", typeof(Type));
        metadata.Columns.Add("ProviderDbType", typeof(int));
        metadata.Columns.Add("ColumnSize", typeof(long));
        metadata.Columns.Add("IsUnsigned", typeof(bool));
        metadata.Columns.Add("ProviderSpecificFacet", typeof(string));
        metadata.Rows.Add("numeric", typeof(decimal), 246, 1000L, false, "retained");
        using var connection = new MetadataConnection(CreateCollections("DataTypes"), metadata);

        var report = AdoNetTypeDiscovery.Discover(connection);

        var type = Assert.Single(report.Types);
        Assert.Equal("numeric", type.DatabaseTypeName);
        Assert.Equal(typeof(decimal).FullName, type.ClrTypeName);
        Assert.Equal(typeof(decimal).AssemblyQualifiedName, type.ClrTypeAssemblyQualifiedName);
        Assert.Equal("246", type.ProviderTypeId);
        Assert.Equal(1000, type.MaximumLength);
        Assert.False(type.IsUnsigned);
        Assert.Equal("retained", type.RawMetadata["ProviderSpecificFacet"]);
        Assert.True(type.IsIndependentDiscovery);
        Assert.True(report.HasIndependentTypeInventory);
        Assert.Empty(report.Diagnostics);
    }

    [Fact]
    public void Discover_ExcludesSelfDerivedSupplementalTypesFromIndependentInventory()
    {
        var supplemental = new SupplementalDiscovery(
        [
            TypeDefinition("integer", isIndependent: false)
        ]);
        using var connection = new MetadataConnection(CreateCollections("Tables"));

        var report = AdoNetTypeDiscovery.Discover(connection, supplemental);

        Assert.Empty(report.Types);
        Assert.True(report.UsesOnlySelfDerivedTypeEvidence);
        Assert.False(report.HasIndependentTypeInventory);
        Assert.Contains(report.Diagnostics, diagnostic => diagnostic.Contains("self-derived evidence", StringComparison.Ordinal));
    }

    [Fact]
    public void Discover_RetainsIndependentSupplementalCatalogTypes()
    {
        var supplemental = new SupplementalDiscovery(
        [
            TypeDefinition("inet", isIndependent: true)
        ]);
        using var connection = new MetadataConnection(CreateCollections("Tables"));

        var report = AdoNetTypeDiscovery.Discover(connection, supplemental);

        Assert.True(report.HasIndependentTypeInventory);
        Assert.False(report.UsesOnlySelfDerivedTypeEvidence);
        Assert.Contains(report.SupplementalTypes, type => type.DatabaseTypeName == "inet" && type.IsIndependentDiscovery);
        Assert.DoesNotContain(report.Diagnostics, diagnostic => diagnostic.Contains("No independent native-type inventory", StringComparison.Ordinal));
    }

    private static DataTable CreateCollections(params string[] names)
    {
        var table = new DataTable();
        table.Columns.Add("CollectionName", typeof(string));
        foreach (var name in names)
        {
            table.Rows.Add(name);
        }

        return table;
    }

    private static DatabaseTypeDefinition TypeDefinition(string typeName, bool isIndependent) => new(
        typeName,
        typeof(string).FullName,
        null,
        null,
        null,
        null,
        true,
        null,
        null,
        "test supplemental catalog",
        new Dictionary<string, object?>())
    {
        IsIndependentDiscovery = isIndependent,
        ClrTypeAssemblyQualifiedName = typeof(string).AssemblyQualifiedName
    };

    private sealed class SupplementalDiscovery(IReadOnlyList<DatabaseTypeDefinition> types) : ISupplementalTypeDiscovery
    {
        public IReadOnlyList<DatabaseTypeDefinition> DiscoverSupplementalTypes(DbConnection openConnection) => types;
    }

    private sealed class MetadataConnection(DataTable collections, DataTable? dataTypes = null) : DbConnection
    {
        private ConnectionState state;

        [AllowNull]
        public override string ConnectionString { get; set; } = string.Empty;

        public override string Database => "metadata-test";

        public override string DataSource => "metadata-test";

        public override string ServerVersion => "1.0";

        public override ConnectionState State => state;

        public override void ChangeDatabase(string databaseName)
        {
        }

        public override void Close() => state = ConnectionState.Closed;

        public override void Open() => state = ConnectionState.Open;

        public override DataTable GetSchema(string collectionName) => collectionName switch
        {
            "MetaDataCollections" => collections,
            "DataTypes" when dataTypes is not null => dataTypes,
            _ => throw new ArgumentException($"Unknown metadata collection '{collectionName}'.", nameof(collectionName))
        };

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            throw new NotSupportedException();

        protected override DbCommand CreateDbCommand() =>
            throw new NotSupportedException();
    }
}
