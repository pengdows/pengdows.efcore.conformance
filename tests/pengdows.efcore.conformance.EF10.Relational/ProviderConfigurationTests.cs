using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class ProviderConfigurationTests
{
    [Fact]
    [BehavioralCoverage("EF10-CONFIG-INVALID-OPTIONS", "Microsoft.EntityFrameworkCore.DbContext.Database", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreated")]
    public async Task DatabaseOperation_RejectsOptionsWithoutProviderRegistration()
    {
        await using var context = new ConformanceDbContext(new DbContextOptions<ConformanceDbContext>());

        var exception = Assert.Throws<InvalidOperationException>(() => context.Database.EnsureCreated());

        Assert.Contains("No database provider has been configured", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
