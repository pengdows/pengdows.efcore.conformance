using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Linq.Expressions;
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
    [BehavioralCoverage("EF10-INTERCEPTION-SAVE-SUPPRESS-SYNC", "Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor.SavingChanges", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task SaveChanges_SynchronousInterceptorCanSuppressPersistence()
    {
        var person = CreatePerson("Sync save suppression");
        var interceptor = new SuppressingSaveChangesInterceptor();

        await using (var initialize = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(initialize, TestContext.Current.CancellationToken);
        }

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            context.People.Add(person);
            Assert.Equal(0, context.SaveChanges());
        }

        Assert.Equal(1, interceptor.SyncSuppressionCalls);
        Assert.Equal(0, interceptor.AsyncSuppressionCalls);
        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AsNoTracking().AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-SAVE-SUPPRESS-ASYNC", "Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor.SavingChangesAsync", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task SaveChangesAsync_AsynchronousInterceptorCanSuppressPersistence()
    {
        var person = CreatePerson("Async save suppression");
        var interceptor = new SuppressingSaveChangesInterceptor();

        await using (var initialize = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(initialize, TestContext.Current.CancellationToken);
        }

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            context.People.Add(person);
            Assert.Equal(0, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        Assert.Equal(0, interceptor.SyncSuppressionCalls);
        Assert.Equal(1, interceptor.AsyncSuppressionCalls);
        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AsNoTracking().AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
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

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-COMMAND-PARAMETER-SYNC", "Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor.ReaderExecuting", "System.Data.Common.DbParameter.Value", "System.Linq.Queryable.ToList")]
    public async Task Query_UsesSynchronousCommandInterceptorParameterChanges()
    {
        var originalValue = Random.Shared.Next(100_000, 1_000_000_000);
        var replacementValue = originalValue + 1;
        var person = CreatePerson("Sync parameter interceptor");
        person.Age = replacementValue;
        await SeedAsync(person);
        var interceptor = new ParameterRewritingCommandInterceptor(originalValue, replacementValue);

        await using var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor));
        var results = context.People.Where(item => item.Age == EF.Parameter(originalValue)).ToList();

        Assert.Collection(results, result =>
        {
            Assert.Equal(person.Id, result.Id);
            Assert.Equal(replacementValue, result.Age);
        });
        Assert.Equal(1, interceptor.ParametersChanged);
        Assert.Equal(0, interceptor.AsyncParametersChanged);
    }

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-COMMAND-PARAMETER-ASYNC", "Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor.ReaderExecutingAsync", "System.Data.Common.DbParameter.Value", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync")]
    public async Task Query_UsesAsynchronousCommandInterceptorParameterChanges()
    {
        var originalValue = Random.Shared.Next(100_000, 1_000_000_000);
        var replacementValue = originalValue + 1;
        var person = CreatePerson("Async parameter interceptor");
        person.Age = replacementValue;
        await SeedAsync(person);
        var interceptor = new ParameterRewritingCommandInterceptor(originalValue, replacementValue);

        await using var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor));
        var results = await context.People
            .Where(item => item.Age == EF.Parameter(originalValue))
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Collection(results, result =>
        {
            Assert.Equal(person.Id, result.Id);
            Assert.Equal(replacementValue, result.Age);
        });
        Assert.Equal(0, interceptor.ParametersChanged);
        Assert.Equal(1, interceptor.AsyncParametersChanged);
    }

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-QUERY-COMPILATION-SYNC", "Microsoft.EntityFrameworkCore.Diagnostics.IQueryExpressionInterceptor.QueryCompilationStarting", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "System.Linq.Queryable.Single")]
    public async Task Query_InvokesQueryCompilationInterceptorSynchronously()
    {
        var person = CreatePerson("Sync query compilation");
        await SeedAsync(person);
        var interceptor = new RecordingQueryExpressionInterceptor();

        await using var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor));
        var result = context.People.Single(item => item.Name == person.Name);

        Assert.Equal(person.Id, result.Id);
        Assert.Equal(1, interceptor.Calls);
    }

    [Fact]
    [BehavioralCoverage("EF10-INTERCEPTION-QUERY-COMPILATION-ASYNC", "Microsoft.EntityFrameworkCore.Diagnostics.IQueryExpressionInterceptor.QueryCompilationStarting", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task Query_InvokesQueryCompilationInterceptorAsynchronously()
    {
        var person = CreatePerson("Async query compilation");
        await SeedAsync(person);
        var interceptor = new RecordingQueryExpressionInterceptor();

        await using var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor));
        var result = await context.People.SingleAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken);

        Assert.Equal(person.Id, result.Id);
        Assert.Equal(1, interceptor.Calls);
    }

    [Fact]
    [CapabilityRequirement("transactions")]
    [BehavioralCoverage("EF10-INTERCEPTION-TRANSACTION-SYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.TransactionStarted", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.TransactionCommitted", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.BeginTransaction", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.Commit")]
    public async Task Transaction_InvokesSynchronousInterceptorCallbacks()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var person = CreatePerson("Sync transaction interceptor");
        var interceptor = new RecordingTransactionInterceptor();

        await using (var initialize = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(initialize, TestContext.Current.CancellationToken);
        }

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            using var transaction = context.Database.BeginTransaction();
            context.People.Add(person);
            Assert.Equal(1, context.SaveChanges());
            transaction.Commit();
        }

        Assert.Equal(1, interceptor.TransactionStartedCalls);
        Assert.Equal(1, interceptor.TransactionCommittedCalls);
        Assert.Equal(1, interceptor.TransactionStartingCalls);
        Assert.Equal(1, interceptor.TransactionCommittingCalls);
        Assert.Equal(0, interceptor.TransactionStartedAsyncCalls);
        Assert.Equal(0, interceptor.TransactionCommittedAsyncCalls);
        Assert.Equal(0, interceptor.TransactionStartingAsyncCalls);
        Assert.Equal(0, interceptor.TransactionCommittingAsyncCalls);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AsNoTracking()
            .AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("transactions")]
    [BehavioralCoverage("EF10-INTERCEPTION-TRANSACTION-ASYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.TransactionStartedAsync", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.TransactionCommittedAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.BeginTransactionAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.CommitAsync")]
    public async Task Transaction_InvokesAsynchronousInterceptorCallbacks()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var person = CreatePerson("Async transaction interceptor");
        var interceptor = new RecordingTransactionInterceptor();

        await using (var initialize = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(initialize, TestContext.Current.CancellationToken);
        }

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            context.People.Add(person);
            Assert.Equal(1, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, interceptor.TransactionStartedCalls);
        Assert.Equal(0, interceptor.TransactionCommittedCalls);
        Assert.Equal(0, interceptor.TransactionStartingCalls);
        Assert.Equal(0, interceptor.TransactionCommittingCalls);
        Assert.Equal(1, interceptor.TransactionStartedAsyncCalls);
        Assert.Equal(1, interceptor.TransactionCommittedAsyncCalls);
        Assert.Equal(1, interceptor.TransactionStartingAsyncCalls);
        Assert.Equal(1, interceptor.TransactionCommittingAsyncCalls);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AsNoTracking()
            .AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("transactions")]
    [CapabilityRequirement("savepoints")]
    [BehavioralCoverage("EF10-INTERCEPTION-SAVEPOINT-SYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.CreatedSavepoint", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.RolledBackToSavepoint", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.CreateSavepoint", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.RollbackToSavepoint")]
    public async Task Savepoint_InvokesSynchronousInterceptorCallbacks()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        ProviderTestRuntime.RequireCapability("savepoints");
        var id = Guid.NewGuid();
        var interceptor = new RecordingTransactionInterceptor();

        await using (var initialize = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(initialize, TestContext.Current.CancellationToken);
        }

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            using var transaction = context.Database.BeginTransaction();
            transaction.CreateSavepoint("before_insert");
            context.People.Add(new Person { Id = id, Name = "Sync savepoint interceptor", Age = 42 });
            Assert.Equal(1, context.SaveChanges());
            transaction.RollbackToSavepoint("before_insert");
            context.ChangeTracker.Clear();
            transaction.Commit();
        }

        Assert.True(interceptor.CreatedSavepointCalls >= 1);
        Assert.Equal(1, interceptor.RolledBackToSavepointCalls);
        Assert.True(interceptor.CreatingSavepointCalls >= 1);
        Assert.Equal(1, interceptor.RollingBackToSavepointCalls);
        Assert.Equal(0, interceptor.CreatedSavepointAsyncCalls);
        Assert.Equal(0, interceptor.RolledBackToSavepointAsyncCalls);
        Assert.Equal(0, interceptor.CreatingSavepointAsyncCalls);
        Assert.Equal(0, interceptor.RollingBackToSavepointAsyncCalls);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AsNoTracking().AnyAsync(item => item.Id == id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("transactions")]
    [CapabilityRequirement("savepoints")]
    [BehavioralCoverage("EF10-INTERCEPTION-SAVEPOINT-ASYNC", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.AddInterceptors", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.CreatedSavepointAsync", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.RolledBackToSavepointAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.CreateSavepointAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.RollbackToSavepointAsync")]
    public async Task Savepoint_InvokesAsynchronousInterceptorCallbacks()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        ProviderTestRuntime.RequireCapability("savepoints");
        var id = Guid.NewGuid();
        var interceptor = new RecordingTransactionInterceptor();

        await using (var initialize = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(initialize, TestContext.Current.CancellationToken);
        }

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await transaction.CreateSavepointAsync("before_insert", TestContext.Current.CancellationToken);
            context.People.Add(new Person { Id = id, Name = "Async savepoint interceptor", Age = 43 });
            Assert.Equal(1, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
            await transaction.RollbackToSavepointAsync("before_insert", TestContext.Current.CancellationToken);
            context.ChangeTracker.Clear();
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, interceptor.CreatedSavepointCalls);
        Assert.Equal(0, interceptor.RolledBackToSavepointCalls);
        Assert.Equal(0, interceptor.CreatingSavepointCalls);
        Assert.Equal(0, interceptor.RollingBackToSavepointCalls);
        Assert.True(interceptor.CreatedSavepointAsyncCalls >= 1);
        Assert.Equal(1, interceptor.RolledBackToSavepointAsyncCalls);
        Assert.True(interceptor.CreatingSavepointAsyncCalls >= 1);
        Assert.Equal(1, interceptor.RollingBackToSavepointAsyncCalls);

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AsNoTracking().AnyAsync(item => item.Id == id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("transactions")]
    [BehavioralCoverage("EF10-INTERCEPTION-TRANSACTION-ROLLBACK-SYNC", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.TransactionRolledBack", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.BeginTransaction", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.Rollback")]
    public async Task Transaction_RollbackInvokesSynchronousInterceptorCallback()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var person = CreatePerson("Sync transaction rollback interceptor");
        var interceptor = new RecordingTransactionInterceptor();

        await using (var initialize = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(initialize, TestContext.Current.CancellationToken);
        }

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            using var transaction = context.Database.BeginTransaction();
            context.People.Add(person);
            Assert.Equal(1, context.SaveChanges());
            transaction.Rollback();
        }

        Assert.Equal(1, interceptor.TransactionRolledBackCalls);
        Assert.Equal(1, interceptor.TransactionRollingBackCalls);
        Assert.Equal(0, interceptor.TransactionRolledBackAsyncCalls);
        Assert.Equal(0, interceptor.TransactionRollingBackAsyncCalls);
        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AsNoTracking().AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("transactions")]
    [BehavioralCoverage("EF10-INTERCEPTION-TRANSACTION-ROLLBACK-ASYNC", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.TransactionRolledBackAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.BeginTransactionAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.RollbackAsync")]
    public async Task Transaction_RollbackInvokesAsynchronousInterceptorCallback()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        var person = CreatePerson("Async transaction rollback interceptor");
        var interceptor = new RecordingTransactionInterceptor();

        await using (var initialize = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(initialize, TestContext.Current.CancellationToken);
        }

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            context.People.Add(person);
            Assert.Equal(1, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, interceptor.TransactionRolledBackCalls);
        Assert.Equal(0, interceptor.TransactionRollingBackCalls);
        Assert.Equal(1, interceptor.TransactionRolledBackAsyncCalls);
        Assert.Equal(1, interceptor.TransactionRollingBackAsyncCalls);
        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.People.AsNoTracking().AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("transactions")]
    [CapabilityRequirement("savepoints")]
    [BehavioralCoverage("EF10-INTERCEPTION-SAVEPOINT-RELEASE-SYNC", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.ReleasedSavepoint", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.CreateSavepoint", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.ReleaseSavepoint")]
    public async Task Savepoint_ReleaseInvokesSynchronousInterceptorCallback()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        ProviderTestRuntime.RequireCapability("savepoints");
        var person = CreatePerson("Sync savepoint release interceptor");
        var interceptor = new RecordingTransactionInterceptor();

        await using (var initialize = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(initialize, TestContext.Current.CancellationToken);
        }

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            using var transaction = context.Database.BeginTransaction();
            transaction.CreateSavepoint("release_point");
            transaction.ReleaseSavepoint("release_point");
            context.People.Add(person);
            Assert.Equal(1, context.SaveChanges());
            transaction.Commit();
        }

        Assert.True(interceptor.ReleasedSavepointCalls >= 1);
        Assert.True(interceptor.ReleasingSavepointCalls >= 1);
        Assert.Equal(0, interceptor.ReleasedSavepointAsyncCalls);
        Assert.Equal(0, interceptor.ReleasingSavepointAsyncCalls);
        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AsNoTracking().AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    [CapabilityRequirement("transactions")]
    [CapabilityRequirement("savepoints")]
    [BehavioralCoverage("EF10-INTERCEPTION-SAVEPOINT-RELEASE-ASYNC", "Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor.ReleasedSavepointAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.CreateSavepointAsync", "Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction.ReleaseSavepointAsync")]
    public async Task Savepoint_ReleaseInvokesAsynchronousInterceptorCallback()
    {
        ProviderTestRuntime.RequireCapability("transactions");
        ProviderTestRuntime.RequireCapability("savepoints");
        var person = CreatePerson("Async savepoint release interceptor");
        var interceptor = new RecordingTransactionInterceptor();

        await using (var initialize = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(initialize, TestContext.Current.CancellationToken);
        }

        await using (var context = ProviderTestRuntime.CreateContext(options => options.AddInterceptors(interceptor)))
        {
            await using var transaction = await context.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            await transaction.CreateSavepointAsync("release_point", TestContext.Current.CancellationToken);
            await transaction.ReleaseSavepointAsync("release_point", TestContext.Current.CancellationToken);
            context.People.Add(person);
            Assert.Equal(1, await context.SaveChangesAsync(TestContext.Current.CancellationToken));
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, interceptor.ReleasedSavepointCalls);
        Assert.Equal(0, interceptor.ReleasingSavepointCalls);
        Assert.True(interceptor.ReleasedSavepointAsyncCalls >= 1);
        Assert.True(interceptor.ReleasingSavepointAsyncCalls >= 1);
        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.People.AsNoTracking().AnyAsync(item => item.Id == person.Id, TestContext.Current.CancellationToken));
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

    private sealed class ParameterRewritingCommandInterceptor(int originalValue, int replacementValue) : DbCommandInterceptor
    {
        public int ParametersChanged { get; private set; }

        public int AsyncParametersChanged { get; private set; }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result)
        {
            ParametersChanged += Rewrite(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            AsyncParametersChanged += Rewrite(command);
            return ValueTask.FromResult(result);
        }

        private int Rewrite(DbCommand command)
        {
            var changed = 0;
            foreach (DbParameter parameter in command.Parameters)
            {
                if (parameter.Value is not null and not DBNull &&
                    Convert.ToInt32(parameter.Value, System.Globalization.CultureInfo.InvariantCulture) == originalValue)
                {
                    parameter.Value = replacementValue;
                    changed++;
                }
            }

            return changed;
        }
    }

    private sealed class SuppressingSaveChangesInterceptor : SaveChangesInterceptor
    {
        public int SyncSuppressionCalls { get; private set; }

        public int AsyncSuppressionCalls { get; private set; }

        public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            SyncSuppressionCalls++;
            return InterceptionResult<int>.SuppressWithResult(0);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            AsyncSuppressionCalls++;
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
        }
    }

    private sealed class RecordingQueryExpressionInterceptor : IQueryExpressionInterceptor
    {
        public int Calls { get; private set; }

        public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
        {
            Calls++;
            return queryExpression;
        }
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

    private sealed class RecordingTransactionInterceptor : DbTransactionInterceptor
    {
        public int TransactionStartedCalls { get; private set; }

        public int TransactionCommittedCalls { get; private set; }

        public int TransactionStartedAsyncCalls { get; private set; }

        public int TransactionCommittedAsyncCalls { get; private set; }

        public int CreatedSavepointCalls { get; private set; }

        public int RolledBackToSavepointCalls { get; private set; }

        public int CreatedSavepointAsyncCalls { get; private set; }

        public int RolledBackToSavepointAsyncCalls { get; private set; }

        public int TransactionRolledBackCalls { get; private set; }

        public int TransactionRolledBackAsyncCalls { get; private set; }

        public int ReleasedSavepointCalls { get; private set; }

        public int ReleasedSavepointAsyncCalls { get; private set; }

        public int TransactionStartingCalls { get; private set; }

        public int TransactionCommittingCalls { get; private set; }

        public int TransactionRollingBackCalls { get; private set; }

        public int CreatingSavepointCalls { get; private set; }

        public int RollingBackToSavepointCalls { get; private set; }

        public int ReleasingSavepointCalls { get; private set; }

        public int TransactionStartingAsyncCalls { get; private set; }

        public int TransactionCommittingAsyncCalls { get; private set; }

        public int TransactionRollingBackAsyncCalls { get; private set; }

        public int CreatingSavepointAsyncCalls { get; private set; }

        public int RollingBackToSavepointAsyncCalls { get; private set; }

        public int ReleasingSavepointAsyncCalls { get; private set; }

        public override InterceptionResult<DbTransaction> TransactionStarting(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result)
        {
            TransactionStartingCalls++;
            return result;
        }

        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
        {
            TransactionStartingAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult TransactionCommitting(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result)
        {
            TransactionCommittingCalls++;
            return result;
        }

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            TransactionCommittingAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult TransactionRollingBack(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result)
        {
            TransactionRollingBackCalls++;
            return result;
        }

        public override ValueTask<InterceptionResult> TransactionRollingBackAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            TransactionRollingBackAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult CreatingSavepoint(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result)
        {
            CreatingSavepointCalls++;
            return result;
        }

        public override ValueTask<InterceptionResult> CreatingSavepointAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            CreatingSavepointAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult RollingBackToSavepoint(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result)
        {
            RollingBackToSavepointCalls++;
            return result;
        }

        public override ValueTask<InterceptionResult> RollingBackToSavepointAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            RollingBackToSavepointAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult ReleasingSavepoint(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result)
        {
            ReleasingSavepointCalls++;
            return result;
        }

        public override ValueTask<InterceptionResult> ReleasingSavepointAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            ReleasingSavepointAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override DbTransaction TransactionStarted(
            DbConnection connection,
            TransactionEndEventData eventData,
            DbTransaction result)
        {
            TransactionStartedCalls++;
            return result;
        }

        public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
            TransactionCommittedCalls++;

        public override ValueTask<DbTransaction> TransactionStartedAsync(
            DbConnection connection,
            TransactionEndEventData eventData,
            DbTransaction result,
            CancellationToken cancellationToken = default)
        {
            TransactionStartedAsyncCalls++;
            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            TransactionCommittedAsyncCalls++;
            return Task.CompletedTask;
        }

        public override void CreatedSavepoint(DbTransaction transaction, TransactionEventData eventData) =>
            CreatedSavepointCalls++;

        public override void RolledBackToSavepoint(DbTransaction transaction, TransactionEventData eventData) =>
            RolledBackToSavepointCalls++;

        public override Task CreatedSavepointAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            CancellationToken cancellationToken = default)
        {
            CreatedSavepointAsyncCalls++;
            return Task.CompletedTask;
        }

        public override Task RolledBackToSavepointAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            CancellationToken cancellationToken = default)
        {
            RolledBackToSavepointAsyncCalls++;
            return Task.CompletedTask;
        }

        public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
            TransactionRolledBackCalls++;

        public override Task TransactionRolledBackAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            TransactionRolledBackAsyncCalls++;
            return Task.CompletedTask;
        }

        public override void ReleasedSavepoint(DbTransaction transaction, TransactionEventData eventData) =>
            ReleasedSavepointCalls++;

        public override Task ReleasedSavepointAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            CancellationToken cancellationToken = default)
        {
            ReleasedSavepointAsyncCalls++;
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
