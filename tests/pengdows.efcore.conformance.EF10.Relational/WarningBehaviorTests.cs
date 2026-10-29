using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class WarningBehaviorTests
{
    private static readonly EventId WarningId = CoreEventId.RowLimitingOperationWithoutOrderByWarning;
    private const string WarningName = nameof(CoreEventId.RowLimitingOperationWithoutOrderByWarning);

    [Fact]
    [BehavioralCoverage("EF10-WARNING-BEHAVIOR-ASYNC", "Microsoft.EntityFrameworkCore.WarningBehavior", "Microsoft.EntityFrameworkCore.WarningBehavior.Log", "Microsoft.EntityFrameworkCore.WarningBehavior.Ignore", "Microsoft.EntityFrameworkCore.WarningBehavior.Throw", "Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.RowLimitingOperationWithoutOrderByWarning", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.ConfigureWarnings", "Microsoft.EntityFrameworkCore.Diagnostics.WarningsConfigurationBuilder.Default", "Microsoft.EntityFrameworkCore.Diagnostics.WarningsConfigurationBuilder.Log", "Microsoft.EntityFrameworkCore.Diagnostics.WarningsConfigurationBuilder.Ignore", "Microsoft.EntityFrameworkCore.Diagnostics.WarningsConfigurationBuilder.Throw")]
    public async Task WarningBehavior_ControlsAsyncQueryWarningHandling()
    {
        await SeedAsync();

        var loggedMessages = new List<string>();
        await using (var logContext = ProviderTestRuntime.CreateContext(options =>
                         options.LogTo(loggedMessages.Add)
                             .ConfigureWarnings(warnings => warnings.Default(WarningBehavior.Log).Log(WarningId))))
        {
            _ = await logContext.People.Skip(1).ToListAsync(TestContext.Current.CancellationToken);
        }
        Assert.Contains(loggedMessages, message => message.Contains(WarningName, StringComparison.Ordinal));

        var ignoredMessages = new List<string>();
        await using (var ignoreContext = ProviderTestRuntime.CreateContext(options =>
                         options.LogTo(ignoredMessages.Add)
                             .ConfigureWarnings(warnings => warnings.Default(WarningBehavior.Ignore).Ignore(WarningId))))
        {
            _ = await ignoreContext.People.Skip(1).ToListAsync(TestContext.Current.CancellationToken);
        }
        Assert.DoesNotContain(ignoredMessages, message => message.Contains(WarningName, StringComparison.Ordinal));

        await using var throwContext = ProviderTestRuntime.CreateContext(options =>
            options.ConfigureWarnings(warnings => warnings.Default(WarningBehavior.Throw).Throw(WarningId)));
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await throwContext.People.Skip(1).ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-WARNING-BEHAVIOR-SYNC", "Microsoft.EntityFrameworkCore.WarningBehavior", "Microsoft.EntityFrameworkCore.WarningBehavior.Log", "Microsoft.EntityFrameworkCore.WarningBehavior.Ignore", "Microsoft.EntityFrameworkCore.WarningBehavior.Throw", "Microsoft.EntityFrameworkCore.Diagnostics.CoreEventId.RowLimitingOperationWithoutOrderByWarning", "Microsoft.EntityFrameworkCore.DbContextOptionsBuilder.ConfigureWarnings", "Microsoft.EntityFrameworkCore.Diagnostics.WarningsConfigurationBuilder.Default", "Microsoft.EntityFrameworkCore.Diagnostics.WarningsConfigurationBuilder.Log", "Microsoft.EntityFrameworkCore.Diagnostics.WarningsConfigurationBuilder.Ignore", "Microsoft.EntityFrameworkCore.Diagnostics.WarningsConfigurationBuilder.Throw")]
    public async Task WarningBehavior_ControlsSynchronousQueryWarningHandling()
    {
        await SeedAsync();

        var loggedMessages = new List<string>();
        await using (var logContext = ProviderTestRuntime.CreateContext(options =>
                         options.LogTo(loggedMessages.Add)
                             .ConfigureWarnings(warnings => warnings.Default(WarningBehavior.Log).Log(WarningId))))
        {
            _ = logContext.People.Skip(1).ToList();
        }
        Assert.Contains(loggedMessages, message => message.Contains(WarningName, StringComparison.Ordinal));

        var ignoredMessages = new List<string>();
        await using (var ignoreContext = ProviderTestRuntime.CreateContext(options =>
                         options.LogTo(ignoredMessages.Add)
                             .ConfigureWarnings(warnings => warnings.Default(WarningBehavior.Ignore).Ignore(WarningId))))
        {
            _ = ignoreContext.People.Skip(1).ToList();
        }
        Assert.DoesNotContain(ignoredMessages, message => message.Contains(WarningName, StringComparison.Ordinal));

        await using var throwContext = ProviderTestRuntime.CreateContext(options =>
            options.ConfigureWarnings(warnings => warnings.Default(WarningBehavior.Throw).Throw(WarningId)));
        Assert.Throws<InvalidOperationException>(() => throwContext.People.Skip(1).ToList());
    }

    private static async Task SeedAsync()
    {
        await using var seed = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
        seed.People.Add(new Person { Id = Guid.NewGuid(), Name = $"Warning {Guid.NewGuid():N}", Age = 42 });
        Assert.Equal(1, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
    }
}
