using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class DeleteBehaviorClientTests
{
    [Fact]
    [BehavioralCoverage("EF10-DELETE-BEHAVIOR-CLIENT-ASYNC", "Microsoft.EntityFrameworkCore.DeleteBehavior.ClientCascade", "Microsoft.EntityFrameworkCore.DeleteBehavior.ClientSetNull", "Microsoft.EntityFrameworkCore.DeleteBehavior.ClientNoAction", "Microsoft.EntityFrameworkCore.DeleteBehavior.NoAction", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync")]
    public async Task ClientDeleteBehaviors_AffectTrackedRowsAsynchronously()
    {
        var ids = await SeedAsync();

        await using (var cascadeContext = ProviderTestRuntime.CreateContext())
        {
            var principal = await cascadeContext.Set<ClientCascadePrincipal>()
                .Include(item => item.Dependents).SingleAsync(item => item.Id == ids.ClientCascade,
                    TestContext.Current.CancellationToken);
            cascadeContext.Remove(principal);
            Assert.Equal(2, await cascadeContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var setNullContext = ProviderTestRuntime.CreateContext())
        {
            var principal = await setNullContext.Set<ClientSetNullPrincipal>()
                .Include(item => item.Dependents).SingleAsync(item => item.Id == ids.ClientSetNull,
                    TestContext.Current.CancellationToken);
            setNullContext.Remove(principal);
            Assert.Equal(2, await setNullContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var noActionContext = ProviderTestRuntime.CreateContext())
        {
            await Assert.ThrowsAnyAsync<DbException>(() => noActionContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM ef10conformance_noactionprincipals WHERE id = {ids.NoAction}",
                TestContext.Current.CancellationToken));
        }

        await using (var clientNoActionContext = ProviderTestRuntime.CreateContext())
        {
            var principal = await clientNoActionContext.Set<ClientNoActionPrincipal>()
                .Include(item => item.Dependents).SingleAsync(item => item.Id == ids.ClientNoAction,
                    TestContext.Current.CancellationToken);
            clientNoActionContext.Remove(principal);
            await Assert.ThrowsAsync<DbUpdateException>(
                () => clientNoActionContext.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await VerifyAsync(ids);
    }

    [Fact]
    [BehavioralCoverage("EF10-DELETE-BEHAVIOR-CLIENT-SYNC", "Microsoft.EntityFrameworkCore.DeleteBehavior.ClientCascade", "Microsoft.EntityFrameworkCore.DeleteBehavior.ClientSetNull", "Microsoft.EntityFrameworkCore.DeleteBehavior.ClientNoAction", "Microsoft.EntityFrameworkCore.DeleteBehavior.NoAction", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolated")]
    public async Task ClientDeleteBehaviors_AffectTrackedRowsSynchronously()
    {
        var ids = await SeedAsync();

        await using (var cascadeContext = ProviderTestRuntime.CreateContext())
        {
            var principal = cascadeContext.Set<ClientCascadePrincipal>()
                .Include(item => item.Dependents).Single(item => item.Id == ids.ClientCascade);
            cascadeContext.Remove(principal);
            Assert.Equal(2, cascadeContext.SaveChanges());
        }

        await using (var setNullContext = ProviderTestRuntime.CreateContext())
        {
            var principal = setNullContext.Set<ClientSetNullPrincipal>()
                .Include(item => item.Dependents).Single(item => item.Id == ids.ClientSetNull);
            setNullContext.Remove(principal);
            Assert.Equal(2, setNullContext.SaveChanges());
        }

        await using (var noActionContext = ProviderTestRuntime.CreateContext())
        {
            Assert.ThrowsAny<DbException>(() => noActionContext.Database.ExecuteSqlInterpolated(
                $"DELETE FROM ef10conformance_noactionprincipals WHERE id = {ids.NoAction}"));
        }

        await using (var clientNoActionContext = ProviderTestRuntime.CreateContext())
        {
            var principal = clientNoActionContext.Set<ClientNoActionPrincipal>()
                .Include(item => item.Dependents).Single(item => item.Id == ids.ClientNoAction);
            clientNoActionContext.Remove(principal);
            Assert.Throws<DbUpdateException>(() => clientNoActionContext.SaveChanges());
        }

        await VerifyAsync(ids);
    }

    private static async Task<(Guid ClientCascade, Guid ClientSetNull, Guid NoAction, Guid ClientNoAction)> SeedAsync()
    {
        var clientCascade = new ClientCascadePrincipal
        {
            Id = Guid.NewGuid(), Dependents = [new ClientCascadeDependent { Id = Guid.NewGuid() }]
        };
        var clientSetNull = new ClientSetNullPrincipal
        {
            Id = Guid.NewGuid(), Dependents = [new ClientSetNullDependent { Id = Guid.NewGuid() }]
        };
        var noAction = new NoActionPrincipal
        {
            Id = Guid.NewGuid(), Dependents = [new NoActionDependent { Id = Guid.NewGuid() }]
        };
        var clientNoAction = new ClientNoActionPrincipal
        {
            Id = Guid.NewGuid(), Dependents = [new ClientNoActionDependent { Id = Guid.NewGuid() }]
        };

        await using var seed = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
        seed.AddRange(clientCascade, clientSetNull, noAction, clientNoAction);
        Assert.Equal(8, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        return (clientCascade.Id, clientSetNull.Id, noAction.Id, clientNoAction.Id);
    }

    private static async Task VerifyAsync((Guid ClientCascade, Guid ClientSetNull, Guid NoAction, Guid ClientNoAction) ids)
    {
        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.Set<ClientCascadePrincipal>().AnyAsync(item => item.Id == ids.ClientCascade,
            TestContext.Current.CancellationToken));
        Assert.False(await verify.Set<ClientCascadeDependent>().AnyAsync(item => item.PrincipalId == ids.ClientCascade,
            TestContext.Current.CancellationToken));
        Assert.False(await verify.Set<ClientSetNullPrincipal>().AnyAsync(item => item.Id == ids.ClientSetNull,
            TestContext.Current.CancellationToken));
        Assert.True(await verify.Set<ClientSetNullDependent>().AnyAsync(
            item => item.PrincipalId == null, TestContext.Current.CancellationToken));
        Assert.True(await verify.Set<NoActionPrincipal>().AnyAsync(item => item.Id == ids.NoAction,
            TestContext.Current.CancellationToken));
        Assert.True(await verify.Set<NoActionDependent>().AnyAsync(item => item.PrincipalId == ids.NoAction,
            TestContext.Current.CancellationToken));
        Assert.True(await verify.Set<ClientNoActionPrincipal>().AnyAsync(item => item.Id == ids.ClientNoAction,
            TestContext.Current.CancellationToken));
        Assert.True(await verify.Set<ClientNoActionDependent>().AnyAsync(item => item.PrincipalId == ids.ClientNoAction,
            TestContext.Current.CancellationToken));
    }
}
