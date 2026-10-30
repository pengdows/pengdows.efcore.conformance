using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class InterceptionTests
{
    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-SAVE-SYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor.SavingChanges", "Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor.SavedChanges", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task SaveChanges_InvokesSynchronousInterceptorCallbacks()
    {
        var interceptor = new RecordingSaveChangesInterceptor();
        var person = CreatePerson("Sync interceptor");

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
            context.People.Add(person);

            Assert.Equal(1, context.SaveChanges());
        }

        Assert.Equal(1, interceptor.SavingChangesCalls);
        Assert.Equal(1, interceptor.SavedChangesCalls);
        Assert.Equal(0, interceptor.SavingChangesAsyncCalls);
        Assert.Equal(0, interceptor.SavedChangesAsyncCalls);
        Assert.Equal(1, interceptor.LastSavedChangesResult);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-SAVE-ASYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor.SavingChangesAsync", "Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor.SavedChangesAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task SaveChangesAsync_InvokesAsynchronousInterceptorCallbacks()
    {
        var interceptor = new RecordingSaveChangesInterceptor();
        var person = CreatePerson("Async interceptor");

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
            context.People.Add(person);

            Assert.Equal(1, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        Assert.Equal(0, interceptor.SavingChangesCalls);
        Assert.Equal(0, interceptor.SavedChangesCalls);
        Assert.Equal(1, interceptor.SavingChangesAsyncCalls);
        Assert.Equal(1, interceptor.SavedChangesAsyncCalls);
        Assert.Equal(1, interceptor.LastSavedChangesResult);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-COMMAND-SYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor.ReaderExecuting", "Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor.ReaderExecuted", "System.Linq.Queryable.Single")]
    public async Task Query_InvokesSynchronousCommandInterceptorCallbacks()
    {
        var person = CreatePerson("Sync command interceptor");
        await SeedAsync(person);
        var interceptor = new RecordingCommandInterceptor();

        await using var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor));
        var actual = context.People.AsNoTracking().Single(item => item.Id == person.Id);

        Assert.Equal(person.Name, actual.Name);
        Assert.Equal(1, interceptor.ReaderExecutingCalls);
        Assert.Equal(1, interceptor.ReaderExecutedCalls);
        Assert.Equal(0, interceptor.ReaderExecutingAsyncCalls);
        Assert.Equal(0, interceptor.ReaderExecutedAsyncCalls);
    }

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-COMMAND-ASYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor.ReaderExecutingAsync", "Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor.ReaderExecutedAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task Query_InvokesAsynchronousCommandInterceptorCallbacks()
    {
        var person = CreatePerson("Async command interceptor");
        await SeedAsync(person);
        var interceptor = new RecordingCommandInterceptor();

        await using var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor));
        var actual = await context.People.AsNoTracking()
            .SingleAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken);

        Assert.Equal(person.Name, actual.Name);
        Assert.Equal(0, interceptor.ReaderExecutingCalls);
        Assert.Equal(0, interceptor.ReaderExecutedCalls);
        Assert.Equal(1, interceptor.ReaderExecutingAsyncCalls);
        Assert.Equal(1, interceptor.ReaderExecutedAsyncCalls);
    }

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-MATERIALIZATION-SYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.IMaterializationInterceptor.CreatingInstance", "Microsoft.EntityFrameworkCore.Diagnostics.IMaterializationInterceptor.CreatedInstance", "Microsoft.EntityFrameworkCore.Diagnostics.IMaterializationInterceptor.InitializingInstance", "Microsoft.EntityFrameworkCore.Diagnostics.IMaterializationInterceptor.InitializedInstance", "System.Linq.Queryable.Single")]
    public async Task Query_InvokesMaterializationInterceptorCallbacksSynchronously()
    {
        var person = CreatePerson("Sync materialization interceptor");
        await SeedAsync(person);
        var interceptor = new RecordingMaterializationInterceptor();

        await using var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor));
        var actual = context.People.AsNoTracking().Single(item => item.Id == person.Id);

        Assert.Equal(person.Name, actual.Name);
        Assert.Equal(new[] { "creating", "created", "initializing", "initialized" }, interceptor.Callbacks);
    }

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-MATERIALIZATION-ASYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.IMaterializationInterceptor.CreatingInstance", "Microsoft.EntityFrameworkCore.Diagnostics.IMaterializationInterceptor.CreatedInstance", "Microsoft.EntityFrameworkCore.Diagnostics.IMaterializationInterceptor.InitializingInstance", "Microsoft.EntityFrameworkCore.Diagnostics.IMaterializationInterceptor.InitializedInstance", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task Query_InvokesMaterializationInterceptorCallbacksAsynchronously()
    {
        var person = CreatePerson("Async materialization interceptor");
        await SeedAsync(person);
        var interceptor = new RecordingMaterializationInterceptor();

        await using var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor));
        var actual = await context.People.AsNoTracking()
            .SingleAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken);

        Assert.Equal(person.Name, actual.Name);
        Assert.Equal(new[] { "creating", "created", "initializing", "initialized" }, interceptor.Callbacks);
    }

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-CONNECTION-SYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor.ConnectionOpening", "Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor.ConnectionOpened", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.OpenConnection", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.CloseConnection")]
    public async Task OpenConnection_InvokesSynchronousConnectionInterceptorCallbacks()
    {
        var interceptor = new RecordingConnectionInterceptor();

        await using var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor));
        context.Database.OpenConnection();
        context.Database.CloseConnection();

        Assert.Equal(1, interceptor.ConnectionOpeningCalls);
        Assert.Equal(1, interceptor.ConnectionOpenedCalls);
        Assert.Equal(0, interceptor.ConnectionOpeningAsyncCalls);
        Assert.Equal(0, interceptor.ConnectionOpenedAsyncCalls);
    }

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-CONNECTION-ASYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor.ConnectionOpeningAsync", "Microsoft.EntityFrameworkCore.Diagnostics.DbConnectionInterceptor.ConnectionOpenedAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.OpenConnectionAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.CloseConnectionAsync")]
    public async Task OpenConnectionAsync_InvokesAsynchronousConnectionInterceptorCallbacks()
    {
        var interceptor = new RecordingConnectionInterceptor();

        await using var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor));
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await context.Database.CloseConnectionAsync();

        Assert.Equal(0, interceptor.ConnectionOpeningCalls);
        Assert.Equal(0, interceptor.ConnectionOpenedCalls);
        Assert.Equal(1, interceptor.ConnectionOpeningAsyncCalls);
        Assert.Equal(1, interceptor.ConnectionOpenedAsyncCalls);
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-INTERCEPTION-NONQUERY-UPDATE-SYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor.NonQueryExecuting", "Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor.NonQueryExecuted", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdate")]
    public async Task ExecuteUpdate_InvokesSynchronousNonQueryInterceptorCallbacks()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var person = CreatePerson("Sync non-query interceptor");
        await SeedAsync(person);
        var interceptor = new RecordingCommandInterceptor();

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            var affected = context.People.Where(item => item.Id == person.Id)
                .ExecuteUpdate(setters => setters.SetProperty(item => item.Age, item => item.Age + 1));
            Assert.Equal(1, affected);
        }

        Assert.Equal(1, interceptor.NonQueryExecutingCalls);
        Assert.Equal(1, interceptor.NonQueryExecutedCalls);
        Assert.Equal(0, interceptor.NonQueryExecutingAsyncCalls);
        Assert.Equal(0, interceptor.NonQueryExecutedAsyncCalls);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(person.Age + 1, await verify.People.AsNoTracking()
            .Where(item => item.Id == person.Id)
            .Select(item => item.Age)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("execute-update")]
    [BehavioralCoverage("EF10-INTERCEPTION-NONQUERY-UPDATE-ASYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor.NonQueryExecutingAsync", "Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor.NonQueryExecutedAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync")]
    public async Task ExecuteUpdateAsync_InvokesAsynchronousNonQueryInterceptorCallbacks()
    {
        ProviderTestRuntime.RequireCapability("execute-update");
        var person = CreatePerson("Async non-query interceptor");
        await SeedAsync(person);
        var interceptor = new RecordingCommandInterceptor();

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            var affected = await context.People.Where(item => item.Id == person.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Age, item => item.Age + 1),
                    TestContext.Current.CancellationToken);
            Assert.Equal(1, affected);
        }

        Assert.Equal(0, interceptor.NonQueryExecutingCalls);
        Assert.Equal(0, interceptor.NonQueryExecutedCalls);
        Assert.Equal(1, interceptor.NonQueryExecutingAsyncCalls);
        Assert.Equal(1, interceptor.NonQueryExecutedAsyncCalls);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.Equal(person.Age + 1, await verify.People.AsNoTracking()
            .Where(item => item.Id == person.Id)
            .Select(item => item.Age)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    private static Person CreatePerson(string prefix) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"{prefix} {Guid.NewGuid():N}",
        Age = 39
    };

    private static async Task SeedAsync(Person person)
    {
        await using var context = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(context, TestContext.Current.CancellationToken);
        context.People.Add(person);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed class RecordingCommandInterceptor : DbCommandInterceptor
    {
        public int ReaderExecutingCalls { get; private set; }

        public int ReaderExecutedCalls { get; private set; }

        public int ReaderExecutingAsyncCalls { get; private set; }

        public int ReaderExecutedAsyncCalls { get; private set; }

        public int NonQueryExecutingCalls { get; private set; }

        public int NonQueryExecutedCalls { get; private set; }

        public int NonQueryExecutingAsyncCalls { get; private set; }

        public int NonQueryExecutedAsyncCalls { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ReaderExecutingCalls++;
            return result;
        }

        public override DbDataReader ReaderExecuted(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result)
        {
            ReaderExecutedCalls++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            ReaderExecutingAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            ReaderExecutedAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result)
        {
            NonQueryExecutingCalls++;
            return result;
        }

        public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
        {
            NonQueryExecutedCalls++;
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            NonQueryExecutingAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            NonQueryExecutedAsyncCalls++;
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RecordingMaterializationInterceptor : IMaterializationInterceptor
    {
        public List<string> Callbacks { get; } = [];

        public InterceptionResult<object> CreatingInstance(
            MaterializationInterceptionData materializationData,
            InterceptionResult<object> result)
        {
            Callbacks.Add("creating");
            return result;
        }

        public object CreatedInstance(MaterializationInterceptionData materializationData, object entity)
        {
            Callbacks.Add("created");
            return entity;
        }

        public InterceptionResult InitializingInstance(
            MaterializationInterceptionData materializationData,
            object entity,
            InterceptionResult result)
        {
            Callbacks.Add("initializing");
            return result;
        }

        public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
        {
            Callbacks.Add("initialized");
            return entity;
        }
    }

    private sealed class RecordingConnectionInterceptor : DbConnectionInterceptor
    {
        public int ConnectionOpeningCalls { get; private set; }

        public int ConnectionOpenedCalls { get; private set; }

        public int ConnectionOpeningAsyncCalls { get; private set; }

        public int ConnectionOpenedAsyncCalls { get; private set; }

        public override InterceptionResult ConnectionOpening(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result)
        {
            ConnectionOpeningCalls++;
            return result;
        }

        public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) =>
            ConnectionOpenedCalls++;

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection,
            ConnectionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            ConnectionOpeningAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            ConnectionOpenedAsyncCalls++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public int SavingChangesCalls { get; private set; }

        public int SavedChangesCalls { get; private set; }

        public int SavingChangesAsyncCalls { get; private set; }

        public int SavedChangesAsyncCalls { get; private set; }

        public int LastSavedChangesResult { get; private set; }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            SavingChangesCalls++;
            return result;
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            SavedChangesCalls++;
            LastSavedChangesResult = result;
            return result;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            SavingChangesAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            SavedChangesAsyncCalls++;
            LastSavedChangesResult = result;
            return ValueTask.FromResult(result);
        }
    }
}
