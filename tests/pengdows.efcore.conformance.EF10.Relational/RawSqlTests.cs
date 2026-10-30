using Microsoft.EntityFrameworkCore;
using System.Data.Common;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("raw-sql")]
public sealed class RawSqlTests
{
    [Fact]
    [BehavioralCoverage("EF10-QUERY-CREATE-DB-COMMAND", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.CreateDbCommand", "System.Data.Common.DbCommand.ExecuteReader", "System.Data.Common.DbDataReader.Read")]
    public async Task CreateDbCommand_ExecutesFilteredQueryWithExpectedValues()
    {
        var marker = $"CreateDbCommand {Guid.NewGuid():N}";
        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.People.AddRange(
                new Person { Id = Guid.NewGuid(), Name = $"{marker} first", Age = 23 },
                new Person { Id = Guid.NewGuid(), Name = $"{marker} second", Age = 31 });
            Assert.Equal(2, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var context = ProviderTestRuntime.CreateContext();
        context.Database.OpenConnection();
        using var command = context.People
            .Where(person => person.Name.StartsWith(marker))
            .OrderBy(person => person.Age)
            .Select(person => new { person.Name, person.Age })
            .CreateDbCommand();
        using var reader = command.ExecuteReader();
        var rows = new List<(string Name, int Age)>();
        while (reader.Read())
        {
            rows.Add((reader.GetString(0), reader.GetInt32(1)));
        }

        Assert.Equal(new[] { ($"{marker} first", 23), ($"{marker} second", 31) }, rows);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-KEYLESS-ASYNC", "Microsoft.EntityFrameworkCore.KeylessAttribute", "Microsoft.EntityFrameworkCore.KeylessAttribute..ctor", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSqlInterpolated", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task KeylessAttribute_MapsParameterizedSqlResultsAsynchronously()
    {
        var id = Guid.NewGuid();
        const string name = "Keyless projection";
        await SeedKeylessRowAsync(id, name);

        await using var context = ProviderTestRuntime.CreateContext();
        var row = await context.KeylessPeople
            .FromSqlInterpolated($"SELECT id, name, age FROM ef10conformancekeylesspeople WHERE id = {id}")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(id, row.Id);
        Assert.Equal(name, row.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-KEYLESS-SYNC", "Microsoft.EntityFrameworkCore.KeylessAttribute", "Microsoft.EntityFrameworkCore.KeylessAttribute..ctor", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSqlInterpolated", "System.Linq.Queryable.Single")]
    public async Task KeylessAttribute_MapsParameterizedSqlResultsSynchronously()
    {
        var id = Guid.NewGuid();
        const string name = "Keyless sync projection";
        await SeedKeylessRowAsync(id, name);

        await using var context = ProviderTestRuntime.CreateContext();
        var row = context.KeylessPeople
            .FromSqlInterpolated($"SELECT id, name, age FROM ef10conformancekeylesspeople WHERE id = {id}")
            .Single();

        Assert.Equal(id, row.Id);
        Assert.Equal(name, row.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-FROM-ASYNC", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSqlInterpolated", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task FromSqlInterpolated_ReturnsParameterizedResult()
    {
        var id = Guid.NewGuid();
        const string value = "O'Brien; DROP TABLE test";
        await SeedAsync(id, value);

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = await context.People
            .FromSqlInterpolated($"SELECT * FROM ef10conformancepeople WHERE id = {id}")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(value, actual.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-FROM-SQL-ASYNC", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSql", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task FromSql_ReturnsParameterizedResultAsynchronously()
    {
        var id = Guid.NewGuid();
        const string value = "FromSql async parameter";
        await SeedAsync(id, value);

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = await context.People
            .FromSql($"SELECT * FROM ef10conformancepeople WHERE id = {id}")
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(id, actual.Id);
        Assert.Equal(value, actual.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-FROM-SQL-SYNC", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSql", "System.Linq.Queryable.Single")]
    public async Task FromSql_ReturnsParameterizedResultSynchronously()
    {
        var id = Guid.NewGuid();
        const string value = "FromSql sync parameter";
        await SeedAsync(id, value);

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = context.People
            .FromSql($"SELECT * FROM ef10conformancepeople WHERE id = {id}")
            .Single();

        Assert.Equal(id, actual.Id);
        Assert.Equal(value, actual.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-FROM-SYNC", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSqlInterpolated", "System.Linq.Queryable.Single")]
    public async Task FromSqlInterpolated_ReturnsResultSynchronously()
    {
        var id = Guid.NewGuid();
        const string value = "Sync O'Brien";
        await SeedAsync(id, value);

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = context.People
            .FromSqlInterpolated($"SELECT * FROM ef10conformancepeople WHERE id = {id}")
            .Single();

        Assert.Equal(value, actual.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-COMMAND-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync")]
    public async Task ExecuteSqlInterpolatedAsync_UpdatesPersistedValues()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Before raw update");

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            Assert.Equal(1, await context.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE ef10conformancepeople SET name = {"After raw update"} WHERE id = {id}",
                TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var name = await verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("After raw update", name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-COMMAND-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolated")]
    public async Task ExecuteSqlInterpolated_UpdatesPersistedValuesSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Before sync raw update");

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            Assert.Equal(1, context.Database.ExecuteSqlInterpolated(
                $"UPDATE ef10conformancepeople SET name = {"After sync raw update"} WHERE id = {id}"));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var name = await verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("After sync raw update", name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-FROM-RAW-ASYNC", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSqlRaw", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task FromSqlRaw_ParametersAndComposesQueryAsynchronously()
    {
        var id = Guid.NewGuid();
        const string value = "FromSqlRaw parameter value";
        await SeedAsync(id, value);

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = await context.People
            .FromSqlRaw("SELECT * FROM ef10conformancepeople WHERE id = {0}", id)
            .Where(person => person.Name == value)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(id, actual.Id);
        Assert.Equal(value, actual.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-FROM-RAW-SYNC", "Microsoft.EntityFrameworkCore.RelationalQueryableExtensions.FromSqlRaw", "System.Linq.Queryable.Single")]
    public async Task FromSqlRaw_ParametersAndComposesQuerySynchronously()
    {
        var id = Guid.NewGuid();
        const string value = "FromSqlRaw sync parameter value";
        await SeedAsync(id, value);

        await using var context = ProviderTestRuntime.CreateContext();
        var actual = context.People
            .FromSqlRaw("SELECT * FROM ef10conformancepeople WHERE id = {0}", id)
            .Where(person => person.Name == value)
            .Single();

        Assert.Equal(id, actual.Id);
        Assert.Equal(value, actual.Name);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-EXECUTE-RAW-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRawAsync")]
    public async Task ExecuteSqlRawAsync_ParametersUpdatePersistedValues()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Before raw command");

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            Assert.Equal(1, await context.Database.ExecuteSqlRawAsync(
                "UPDATE ef10conformancepeople SET name = {0} WHERE id = {1}",
                ["After raw command", id],
                TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("After raw command", await verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-EXECUTE-RAW-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlRaw")]
    public async Task ExecuteSqlRaw_ParametersUpdatePersistedValuesSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Before sync raw command");

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            Assert.Equal(1, context.Database.ExecuteSqlRaw(
                "UPDATE ef10conformancepeople SET name = {0} WHERE id = {1}",
                ["After sync raw command", id]));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("After sync raw command", await verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-EXECUTE-FORMATTABLE-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlAsync")]
    public async Task ExecuteSqlAsync_UpdatesPersistedValues()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Before FormattableString command");

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            Assert.Equal(1, await context.Database.ExecuteSqlAsync(
                $"UPDATE ef10conformancepeople SET name = {"After FormattableString command"} WHERE id = {id}",
                TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("After FormattableString command", await verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-EXECUTE-FORMATTABLE-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSql")]
    public async Task ExecuteSql_UpdatesPersistedValuesSynchronously()
    {
        var id = Guid.NewGuid();
        await SeedAsync(id, "Before sync FormattableString command");

        await using (var context = ProviderTestRuntime.CreateContext())
        {
            Assert.Equal(1, context.Database.ExecuteSql(
                $"UPDATE ef10conformancepeople SET name = {"After sync FormattableString command"} WHERE id = {id}"));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal("After sync FormattableString command", verify.People.AsNoTracking()
            .Where(person => person.Id == id)
            .Select(person => person.Name)
            .Single());
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-SCALAR-RAW-ASYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SqlQueryRaw", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task SqlQueryRaw_ReturnsParameterizedScalarAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("raw-sql");
        const int expected = 8675309;
        await using var context = ProviderTestRuntime.CreateContext();

        var actual = await context.Database
            .SqlQueryRaw<int>("SELECT {0} AS Value", expected)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-RAW-SQL-SCALAR-RAW-SYNC", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SqlQueryRaw", "System.Linq.Queryable.Single")]
    public async Task SqlQueryRaw_ReturnsParameterizedScalarSynchronously()
    {
        ProviderTestRuntime.RequireCapability("raw-sql");
        const int expected = 3141592;
        await using var context = ProviderTestRuntime.CreateContext();

        var actual = context.Database
            .SqlQueryRaw<int>("SELECT {0} AS Value", expected)
            .Single();

        Assert.Equal(expected, actual);
    }

    private static async Task SeedAsync(Guid id, string name)
    {
        ProviderTestRuntime.RequireCapability("raw-sql");
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(new Person { Id = id, Name = name, Age = 29 });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SeedKeylessRowAsync(Guid id, string name)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        Assert.Equal(1, await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO ef10conformancekeylesspeople (id, name, age) VALUES ({id}, {name}, {42})",
            TestContext.Current.CancellationToken));
    }
}
