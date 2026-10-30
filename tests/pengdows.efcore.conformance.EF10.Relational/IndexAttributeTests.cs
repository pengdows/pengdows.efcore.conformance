using Microsoft.EntityFrameworkCore;
using System.Data;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class IndexAttributeTests
{
    [Fact]
    [CapabilityRequirement("index-schema-metadata")]
    [BehavioralCoverage("EF10-INDEX-ATTRIBUTE-NAME", "Microsoft.EntityFrameworkCore.IndexAttribute.Name")]
    public async Task NamedIndexAttribute_CreatesIndexWithDeclaredName()
    {
        ProviderTestRuntime.RequireCapability("index-schema-metadata");
        await using (var context = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        }

        await using var connection = ProviderTestRuntime.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var indexes = connection.GetSchema("Indexes");
        var indexRows = indexes.Rows.Cast<DataRow>().Where(row =>
            row.ItemArray.Any(value => string.Equals(
                Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
                "IX_EF10_UniqueValueRecord_Value",
                StringComparison.OrdinalIgnoreCase))).ToArray();

        Assert.NotEmpty(indexRows);

    }

    [Fact]
    [BehavioralCoverage("EF10-INDEX-ATTRIBUTE-UNIQUE-ASYNC", "Microsoft.EntityFrameworkCore.IndexAttribute", "Microsoft.EntityFrameworkCore.IndexAttribute..ctor", "Microsoft.EntityFrameworkCore.IndexAttribute.PropertyNames", "Microsoft.EntityFrameworkCore.IndexAttribute.IsUnique", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.DbUpdateException", "Microsoft.EntityFrameworkCore.DbUpdateException.Entries")]
    public async Task UniqueIndexAttribute_RejectsDuplicateValuesAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var value = $"unique-{Guid.NewGuid():N}";
        var original = new UniqueValueRecord { Id = Guid.NewGuid(), Value = value };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.Set<UniqueValueRecord>().Add(original);
            Assert.Equal(1, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        var duplicate = new UniqueValueRecord { Id = Guid.NewGuid(), Value = value };
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            write.Set<UniqueValueRecord>().Add(duplicate);
            var exception = await Assert.ThrowsAsync<DbUpdateException>(
                () => write.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.Same(duplicate, Assert.Single(exception.Entries).Entity);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var stored = await verify.Set<UniqueValueRecord>().AsNoTracking()
            .Where(record => record.Value == value)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(new[] { original.Id }, stored.Select(record => record.Id));
    }

    [Fact]
    [BehavioralCoverage("EF10-INDEX-ATTRIBUTE-UNIQUE-SYNC", "Microsoft.EntityFrameworkCore.IndexAttribute", "Microsoft.EntityFrameworkCore.IndexAttribute..ctor", "Microsoft.EntityFrameworkCore.IndexAttribute.PropertyNames", "Microsoft.EntityFrameworkCore.IndexAttribute.IsUnique", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.DbUpdateException", "Microsoft.EntityFrameworkCore.DbUpdateException.Entries")]
    public async Task UniqueIndexAttribute_RejectsDuplicateValuesSynchronously()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var value = $"unique-{Guid.NewGuid():N}";
        var original = new UniqueValueRecord { Id = Guid.NewGuid(), Value = value };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.Set<UniqueValueRecord>().Add(original);
            Assert.Equal(1, seed.SaveChanges());
        }

        var duplicate = new UniqueValueRecord { Id = Guid.NewGuid(), Value = value };
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            write.Set<UniqueValueRecord>().Add(duplicate);
            var exception = Assert.Throws<DbUpdateException>(() => write.SaveChanges());
            Assert.Same(duplicate, Assert.Single(exception.Entries).Entity);
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var stored = verify.Set<UniqueValueRecord>().AsNoTracking()
            .Where(record => record.Value == value)
            .ToList();
        Assert.Equal(new[] { original.Id }, stored.Select(record => record.Id));
    }
}
