using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class DatabaseSeedingTests
{
    [Fact]
    [BehavioralCoverage("EF10-DATABASE-SEEDING-SYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.UseSeeding", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreated", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task UseSeeding_RunsForAnExistingDatabaseAndPersistsThroughEf()
    {
        await using (var setup = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(setup, TestContext.Current.CancellationToken);
        }

        var id = Guid.NewGuid();
        var name = $"Synchronous seeder {id:N}";
        var calls = 0;
        var storeWasCreated = true;
        await using (var context = ProviderTestRuntime.CreateContext(options =>
                     options.UseSeeding((db, created) =>
                     {
                         calls++;
                         storeWasCreated = created;
                         db.Set<Person>().Add(new Person { Id = id, Name = name, Age = 70 });
                         db.SaveChanges();
                     })))
        {
            Assert.False(context.Database.EnsureCreated());
        }

        Assert.Equal(1, calls);
        Assert.False(storeWasCreated);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(name, await verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-DATABASE-SEEDING-ASYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.UseAsyncSeeding", "Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.EnsureCreatedAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task UseAsyncSeeding_RunsForAnExistingDatabaseAndPersistsThroughEf()
    {
        await using (var setup = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(setup, TestContext.Current.CancellationToken);
        }

        var id = Guid.NewGuid();
        var name = $"Asynchronous seeder {id:N}";
        var calls = 0;
        var storeWasCreated = true;
        await using (var context = ProviderTestRuntime.CreateContext(options =>
                     options.UseAsyncSeeding(async (db, created, cancellationToken) =>
                     {
                         calls++;
                         storeWasCreated = created;
                         db.Set<Person>().Add(new Person { Id = id, Name = name, Age = 71 });
                         await db.SaveChangesAsync(cancellationToken);
                     })))
        {
            Assert.False(await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken));
        }

        Assert.Equal(1, calls);
        Assert.False(storeWasCreated);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(name, await verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }
}
