using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class DbContextFactoryTests
{
    [Fact]
    [BehavioralCoverage("EF10-DBCONTEXT-DI-SYNC", "Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions.AddDbContext")]
    public async Task AddDbContext_ResolvesConfiguredContextFromScope()
    {
        var person = NewPerson("Synchronous DI context");
        var services = new ServiceCollection();
        services.AddDbContext<ConformanceDbContext>(options =>
            ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider();

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var write = scope.ServiceProvider.GetRequiredService<ConformanceDbContext>();
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.People.Add(person);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verifyScope = serviceProvider.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<ConformanceDbContext>();
        var actual = verify.People.AsNoTracking().Single(item => item.Id == person.Id);
        Assert.Equal(person.Name, actual.Name);
        Assert.Equal(person.Age, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-DBCONTEXT-DI-ASYNC", "Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions.AddDbContext")]
    public async Task AddDbContext_ResolvesConfiguredContextFromScopeAsynchronously()
    {
        var person = NewPerson("Asynchronous DI context");
        var services = new ServiceCollection();
        services.AddDbContext<ConformanceDbContext>(options =>
            ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var write = scope.ServiceProvider.GetRequiredService<ConformanceDbContext>();
            await ProviderTestRuntime.InitializeAsync(write, cancellationToken);
            write.People.Add(person);
            Assert.Equal(1, await write.SaveChangesAsync(cancellationToken));
        }

        await using var verifyScope = serviceProvider.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<ConformanceDbContext>();
        var actual = await verify.People.AsNoTracking().SingleAsync(
            item => item.Id == person.Id,
            cancellationToken);
        Assert.Equal(person.Name, actual.Name);
        Assert.Equal(person.Age, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-DBCONTEXT-FACTORY-SYNC", "Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions.AddDbContextFactory", "Microsoft.EntityFrameworkCore.IDbContextFactory`1.CreateDbContext")]
    public async Task DbContextFactory_CreatesContextsThatShareConfiguredProviderSynchronously()
    {
        var person = NewPerson("Synchronous context factory");
        var services = new ServiceCollection();
        services.AddDbContextFactory<ConformanceDbContext>(options =>
            ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<ConformanceDbContext>>();

        await using (var write = factory.CreateDbContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.People.Add(person);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verify = factory.CreateDbContext();
        var actual = verify.People.AsNoTracking().Single(item => item.Id == person.Id);
        Assert.Equal(person.Name, actual.Name);
        Assert.Equal(person.Age, actual.Age);
    }

    [Fact]
    [BehavioralCoverage("EF10-DBCONTEXT-FACTORY-ASYNC", "Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions.AddDbContextFactory", "Microsoft.EntityFrameworkCore.IDbContextFactory`1.CreateDbContextAsync")]
    public async Task DbContextFactory_CreatesContextsThatShareConfiguredProviderAsynchronously()
    {
        var person = NewPerson("Asynchronous context factory");
        var services = new ServiceCollection();
        services.AddDbContextFactory<ConformanceDbContext>(options =>
            ProviderTestRuntime.Adapter.Configure(options, ProviderTestRuntime.ConnectionString));
        await using var serviceProvider = services.BuildServiceProvider();
        var factory = serviceProvider.GetRequiredService<IDbContextFactory<ConformanceDbContext>>();
        var cancellationToken = TestContext.Current.CancellationToken;

        await using (var write = await factory.CreateDbContextAsync(cancellationToken))
        {
            await ProviderTestRuntime.InitializeAsync(write, cancellationToken);
            write.People.Add(person);
            Assert.Equal(1, await write.SaveChangesAsync(cancellationToken));
        }

        await using var verify = await factory.CreateDbContextAsync(cancellationToken);
        var actual = await verify.People.AsNoTracking().SingleAsync(
            item => item.Id == person.Id,
            cancellationToken);
        Assert.Equal(person.Name, actual.Name);
        Assert.Equal(person.Age, actual.Age);
    }

    private static Person NewPerson(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{name} {Guid.NewGuid():N}",
        Age = 44
    };
}
