using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class DbSetApiTests
{
    [Fact]
    [BehavioralCoverage("EF10-DBSET-AS-QUERYABLE", "Microsoft.EntityFrameworkCore.DbContext.Set", "Microsoft.EntityFrameworkCore.DbSet<TEntity> Set<TEntity>() where TEntity : class", "Microsoft.EntityFrameworkCore.DbSet`1.AsQueryable", "Microsoft.EntityFrameworkCore.DbSet`1.Entry")]
    public async Task AsQueryable_ComposesAndDbSetEntryReturnsTrackedEntityEntry()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "DbSet queryable entry", 51);

        await using var context = ProviderTestRuntime.CreateContext();
        var person = context.Set<Person>().AsQueryable()
            .Where(item => item.Id == id)
            .Single();
        var entry = context.People.Entry(person);

        Assert.Equal("DbSet queryable entry", person.Name);
        Assert.Equal(EntityState.Unchanged, entry.State);
        Assert.Same(person, entry.Entity);
    }

    [Fact]
    [BehavioralCoverage("EF10-DBSET-NAMED-SHARED-TYPE", "Microsoft.EntityFrameworkCore.DbContext.Set", "Microsoft.EntityFrameworkCore.DbSet<TEntity> Set<TEntity>(System.String name) where TEntity : class")]
    public async Task SetByName_UsesTheRequestedSharedEntityType()
    {
        var id = Guid.NewGuid();
        var name = $"named-set-{Guid.NewGuid():N}";
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            var set = write.Set<Dictionary<string, object>>("EF10NamedPerson");
            set.Add(new Dictionary<string, object> { ["Id"] = id, ["Name"] = name });
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.Set<Dictionary<string, object>>("EF10NamedPerson")
            .Where(row => EF.Property<Guid>(row, "Id") == id)
            .Select(row => EF.Property<string>(row, "Name"))
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(name, actual);
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
    [BehavioralCoverage("EF10-DBSET-ASYNC-ENUMERATOR", "Microsoft.EntityFrameworkCore.DbSet`1.GetAsyncEnumerator", "System.Collections.Generic.IAsyncEnumerator`1.MoveNextAsync", "System.Collections.Generic.IAsyncDisposable.DisposeAsync")]
    public async Task GetAsyncEnumerator_MovesThroughAndDisposesQueryResults()
    {
        var batch = Guid.NewGuid().ToString("N");
        await SeedAsync(new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-one", Age = 1 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-two", Age = 2 }
        });

        await using var context = ProviderTestRuntime.CreateContext();
        await using var enumerator = context.People.GetAsyncEnumerator(TestContext.Current.CancellationToken);
        var names = new List<string>();
        while (await enumerator.MoveNextAsync())
        {
            var name = enumerator.Current.Name;
            if (name.StartsWith(batch, StringComparison.Ordinal))
            {
                names.Add(name);
            }
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
    [BehavioralCoverage("EF10-DBSET-LOCAL-QUERY-SYNC", "Microsoft.EntityFrameworkCore.DbSet`1.Local", "System.Linq.Queryable.Single")]
    public async Task Local_IncludesEntitiesMaterializedBySynchronousQueries()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "DbSet local sync query", 55);

        await using var context = ProviderTestRuntime.CreateContext();
        Assert.Empty(context.People.Local);

        var person = context.People.Single(item => item.Id == id);

        Assert.Contains(person, context.People.Local);
        Assert.Same(person, context.People.Local.Single(item => item.Id == id));
    }

    [Fact]
    [BehavioralCoverage("EF10-DBSET-LOCAL-QUERY-ASYNC", "Microsoft.EntityFrameworkCore.DbSet`1.Local", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task Local_IncludesEntitiesMaterializedByAsynchronousQueries()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "DbSet local async query", 56);

        await using var context = ProviderTestRuntime.CreateContext();
        Assert.Empty(context.People.Local);

        var person = await context.People.SingleAsync(item => item.Id == id, TestContext.Current.CancellationToken);

        Assert.Contains(person, context.People.Local);
        Assert.Same(person, context.People.Local.Single(item => item.Id == id));
    }

    [Fact]
    [BehavioralCoverage("EF10-DBSET-ATTACH-RANGE", "Microsoft.EntityFrameworkCore.DbSet`1.AttachRange", "System.Void AttachRange(TEntity[] entities)", "System.Void AttachRange(System.Collections.Generic.IEnumerable<TEntity> entities)", "Microsoft.EntityFrameworkCore.DbContext.Entry", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
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
            var detached = existing.Take(1).Select(person => new Person
            {
                Id = person.Id,
                Name = person.Name,
                Age = person.Age
            }).ToArray();

            update.People.AttachRange(detached);
            Assert.All(detached, person => Assert.Equal(EntityState.Unchanged, update.Entry(person).State));
            var enumerableDetached = existing.Skip(1).Select(person => new Person { Id = person.Id, Name = person.Name, Age = person.Age }).ToList();
            update.People.AttachRange(enumerableDetached.AsEnumerable());
            Assert.All(enumerableDetached, person => Assert.Equal(EntityState.Unchanged, update.Entry(person).State));
            detached[0].Age++;
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(existing[0].Age + 1, await verify.People.AsNoTracking()
            .Where(person => person.Id == existing[0].Id)
            .Select(person => person.Age)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-DBSET-ADD-RANGE-SYNC", "Microsoft.EntityFrameworkCore.DbSet`1.AddRange", "System.Void AddRange(TEntity[] entities)", "System.Void AddRange(System.Collections.Generic.IEnumerable<TEntity> entities)", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Count")]
    public async Task AddRange_PersistsEveryEntitySynchronously()
    {
        var batch = Guid.NewGuid().ToString("N");
        var people = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-one", Age = 61 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-two", Age = 62 }
        };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.People.AddRange(people);
            write.People.AddRange(people.Select(person => new Person { Id = Guid.NewGuid(), Name = person.Name + "-enumerable", Age = person.Age }).AsEnumerable());
            Assert.Equal(4, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(4, verify.People.Count(person => person.Name.StartsWith(batch)));
    }

    [Fact]
    [BehavioralCoverage("EF10-DBSET-ADD-RANGE-ASYNC", "Microsoft.EntityFrameworkCore.DbSet`1.AddRangeAsync", "System.Threading.Tasks.Task AddRangeAsync(TEntity[] entities)", "System.Threading.Tasks.Task AddRangeAsync(System.Collections.Generic.IEnumerable<TEntity> entities, System.Threading.CancellationToken cancellationToken = null)", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync")]
    public async Task AddRangeAsync_PersistsEveryEntity()
    {
        var batch = Guid.NewGuid().ToString("N");
        var people = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-one", Age = 63 },
            new Person { Id = Guid.NewGuid(), Name = $"{batch}-two", Age = 64 }
        };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            await write.People.AddRangeAsync(people, TestContext.Current.CancellationToken);
            var enumerablePeople = people.Select(person => new Person { Id = Guid.NewGuid(), Name = person.Name + "-enumerable", Age = person.Age }).ToList();
            await write.People.AddRangeAsync(enumerablePeople.AsEnumerable(), TestContext.Current.CancellationToken);
            Assert.Equal(4, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(4, await verify.People.CountAsync(
            person => person.Name.StartsWith(batch),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-DBSET-UPDATE-RANGE", "Microsoft.EntityFrameworkCore.DbSet`1.UpdateRange", "System.Void UpdateRange(TEntity[] entities)", "System.Void UpdateRange(System.Collections.Generic.IEnumerable<TEntity> entities)", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.ToArray")]
    public async Task UpdateRange_PersistsModifiedValuesForEveryEntity()
    {
        var existing = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = "DbSet update range one", Age = 65 },
            new Person { Id = Guid.NewGuid(), Name = "DbSet update range two", Age = 66 },
            new Person { Id = Guid.NewGuid(), Name = "DbSet update range three", Age = 67 },
            new Person { Id = Guid.NewGuid(), Name = "DbSet update range four", Age = 68 }
        };
        await SeedAsync(existing);
        var detached = existing.Take(2).Select(person => new Person
        {
            Id = person.Id,
            Name = $"{person.Name} updated",
            Age = person.Age + 10
        }).ToArray();

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            update.People.UpdateRange(detached);
            Assert.All(detached, person => Assert.Equal(EntityState.Modified, update.Entry(person).State));
            var enumerableDetached = existing.Skip(2).Select(person => new Person { Id = person.Id, Name = person.Name + " updated enumerable", Age = person.Age + 11 }).ToList();
            update.People.UpdateRange(enumerableDetached.AsEnumerable());
            Assert.All(enumerableDetached, person => Assert.Equal(EntityState.Modified, update.Entry(person).State));
            Assert.Equal(4, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.People.AsNoTracking()
            .Where(person => existing.Select(item => item.Id).Contains(person.Id))
            .OrderBy(person => person.Age)
            .ToArray();
        var expectedNames = existing.Take(2).Select(person => person.Name + " updated")
            .Concat(existing.Skip(2).Select(person => person.Name + " updated enumerable"))
            .Order(StringComparer.Ordinal);
        Assert.Equal(expectedNames, actual.Select(person => person.Name).Order(StringComparer.Ordinal));
        Assert.Equal(new[] { 75, 76, 78, 79 }, actual.Select(person => person.Age));
    }

    [Fact]
    [BehavioralCoverage("EF10-DBSET-REMOVE-RANGE", "Microsoft.EntityFrameworkCore.DbSet`1.RemoveRange", "System.Void RemoveRange(TEntity[] entities)", "System.Void RemoveRange(System.Collections.Generic.IEnumerable<TEntity> entities)", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Count")]
    public async Task RemoveRange_DeletesEveryEntity()
    {
        var existing = new[]
        {
            new Person { Id = Guid.NewGuid(), Name = "DbSet remove range one", Age = 67 },
            new Person { Id = Guid.NewGuid(), Name = "DbSet remove range two", Age = 68 },
            new Person { Id = Guid.NewGuid(), Name = "DbSet remove range three", Age = 69 },
            new Person { Id = Guid.NewGuid(), Name = "DbSet remove range four", Age = 70 }
        };
        await SeedAsync(existing);

        await using (var remove = ProviderTestRuntime.CreateContext())
        {
            var tracked = existing.Take(2).Select(person => new Person { Id = person.Id }).ToArray();
            remove.People.AttachRange(tracked);
            remove.People.RemoveRange(tracked);
            var enumerableTracked = existing.Skip(2).Select(person => new Person { Id = person.Id }).ToList();
            remove.People.AttachRange(enumerableTracked.AsEnumerable());
            remove.People.RemoveRange(enumerableTracked.AsEnumerable());
            Assert.Equal(4, remove.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(0, verify.People.Count(person => existing.Select(item => item.Id).Contains(person.Id)));
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
