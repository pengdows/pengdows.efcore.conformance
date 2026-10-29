using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class DbFunctionTests
{
    [Fact]
    [BehavioralCoverage("EF10-DB-FUNCTION-ATTRIBUTE-ASYNC", "Microsoft.EntityFrameworkCore.DbFunctionAttribute", "Microsoft.EntityFrameworkCore.DbFunctionAttribute..ctor", "Microsoft.EntityFrameworkCore.DbFunctionAttribute.Name", "Microsoft.EntityFrameworkCore.DbFunctionAttribute.IsBuiltIn", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "System.Linq.Queryable.Where", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task DbFunctionAttribute_TranslatesBuiltInFunctionInAsyncPredicate()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var expected = new Person { Id = Guid.NewGuid(), Name = "MIXED Case", Age = 37 };
        await SeedAsync(expected);

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = await context.People.AsNoTracking()
            .Where(person => ConformanceDbContext.Lower(person.Name) == "mixed case")
            .SingleAsync(person => person.Id == expected.Id, TestContext.Current.CancellationToken);

        Assert.Equal(expected.Name, actual.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-DB-FUNCTION-ATTRIBUTE-SYNC", "Microsoft.EntityFrameworkCore.DbFunctionAttribute", "Microsoft.EntityFrameworkCore.DbFunctionAttribute..ctor", "Microsoft.EntityFrameworkCore.DbFunctionAttribute.Name", "Microsoft.EntityFrameworkCore.DbFunctionAttribute.IsBuiltIn", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Where", "System.Linq.Queryable.Single")]
    public async Task DbFunctionAttribute_TranslatesBuiltInFunctionInSyncPredicate()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var expected = new Person { Id = Guid.NewGuid(), Name = "ANOTHER Value", Age = 39 };
        await SeedAsync(expected);

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = context.People.AsNoTracking()
            .Where(person => ConformanceDbContext.Lower(person.Name) == "another value")
            .Single(person => person.Id == expected.Id);

        Assert.Equal(expected.Name, actual.Name);
    }

    private static async Task SeedAsync(Person person)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(person);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
