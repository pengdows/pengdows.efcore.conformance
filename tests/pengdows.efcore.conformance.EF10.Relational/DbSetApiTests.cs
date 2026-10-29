using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class DbSetApiTests
{
    [Fact]
    [BehavioralCoverage("EF10-DBSET-AS-QUERYABLE", "Microsoft.EntityFrameworkCore.DbSet`1.AsQueryable", "Microsoft.EntityFrameworkCore.DbSet`1.Entry")]
    public async Task AsQueryable_ComposesAndDbSetEntryReturnsTrackedEntityEntry()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "DbSet queryable entry", 51);

        await using var context = ProviderTestRuntime.CreateContext();
        var person = context.People.AsQueryable()
            .Where(item => item.Id == id)
            .Single();
        var entry = context.People.Entry(person);

        Assert.Equal("DbSet queryable entry", person.Name);
        Assert.Equal(EntityState.Unchanged, entry.State);
        Assert.Same(person, entry.Entity);
    }

    [Fact]
    [BehavioralCoverage("EF10-DBSET-AS-ASYNC-ENUMERABLE", "Microsoft.EntityFrameworkCore.DbSet`1.AsAsyncEnumerable")]
    public async Task AsAsyncEnumerable_StreamsEveryEntityFromDbSet()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-one", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-two", Age = 2 }
        });

        await using var context = ProviderTestRuntime.CreateContext();
        var names = new List<string>();
        await foreach (var person in context.People
                           .Where(item => item.Name.StartsWith(batch))
                           .AsAsyncEnumerable()
                           .WithCancellation(TestContext.Current.CancellationToken))
        {
            names.Add(person.Name);
        }

        Assert.Equal(new[] { $"{batch}-one", $"{batch}-two" }, names.Order(StringComparer.Ordinal));
    }

    [Fact]
    [BehavioralCoverage("EF10-DBSET-LOCAL", "Microsoft.EntityFrameworkCore.DbSet`1.Local")]
    public async Task Local_ReflectsAddedAndDeletedTrackedEntities()
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        var person = new Person { Id = Guid.NewGuid(), Name = "Local tracked entity", Age = 52 };

        context.People.Add(person);
        Assert.Contains(person, context.People.Local);
        Assert.Equal(1, await context.SaveChangesAsync(TestContext.Current.CancellationToken));

        context.People.Remove(person);
        Assert.DoesNotContain(person, context.People.Local);
    }

    [Fact]
    [BehavioralCoverage("EF10-DBSET-ATTACH-RANGE", "Microsoft.EntityFrameworkCore.DbSet`1.AttachRange", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task AttachRange_TracksDetachedEntitiesAndPersistsChanges()
    {
        var existing = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = "DbSet attach one", Age = 53 },
            new Person { Id = Guid.NewGuid(), Name = "DbSet attach two", Age = 54 }
        };
        await SeedAsync(existing);

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var detached = existing.Select(person => new Person
            {
                Id = person.Id,
                Name = person.Name,
                Age = person.Age
            }).ToArray();

            update.People.AttachRange(detached);
            Assert.All(detached, person => Assert.Equal(EntityState.Unchanged, update.Entry(person).State));
            detached[0].Age++;
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(existing[0].Age + 1, await verify.People.AsNoTracking()
            .Where(person => person.Id == existing[0].Id)
            .Select(person => person.Age)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    private static async Task SeedAsync(Guid id, string name, int age)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = name, Age = age });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SeedAsync(IEnumerable<Person> people)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.AddRange(people);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
