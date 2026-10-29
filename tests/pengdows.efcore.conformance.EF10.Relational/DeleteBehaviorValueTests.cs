using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class DeleteBehaviorValueTests
{
    [Fact]
    [BehavioralCoverage("EF10-DELETE-BEHAVIOR-CASCADE-ASYNC", "Microsoft.EntityFrameworkCore.DeleteBehavior", "Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade", "Microsoft.EntityFrameworkCore.DeleteBehavior.SetNull", "Microsoft.EntityFrameworkCore.DeleteBehaviorAttribute", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync")]
    public async Task DeleteBehaviorCascade_RemovesDependentOnDatabaseDeleteAsynchronously()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var (cascade, setNull) = await SeedAsync();

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            Assert.Equal(1, await delete.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM ef10conformance_cascadeprincipals WHERE id = {cascade.Id}",
                TestContext.Current.CancellationToken));
            Assert.Equal(1, await delete.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM ef10conformance_setnullprincipals WHERE id = {setNull.Id}",
                TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(await verify.Set<CascadePrincipal>().AnyAsync(item => item.Id == cascade.Id,
            TestContext.Current.CancellationToken));
        Assert.False(await verify.Set<CascadeDependent>().AnyAsync(item => item.PrincipalId == cascade.Id,
            TestContext.Current.CancellationToken));
        var orphan = await verify.Set<SetNullDependent>().AsNoTracking()
            .SingleAsync(item => item.Id == setNull.Dependents.Single().Id,
                TestContext.Current.CancellationToken);
        Assert.Null(orphan.PrincipalId);
    }

    [Fact]
    [BehavioralCoverage("EF10-DELETE-BEHAVIOR-CASCADE-SYNC", "Microsoft.EntityFrameworkCore.DeleteBehavior", "Microsoft.EntityFrameworkCore.DeleteBehavior.Cascade", "Microsoft.EntityFrameworkCore.DeleteBehavior.SetNull", "Microsoft.EntityFrameworkCore.DeleteBehaviorAttribute", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolated")]
    public async Task DeleteBehaviorCascade_RemovesDependentOnDatabaseDeleteSynchronously()
    {
        var (cascade, setNull) = await SeedAsync();

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            Assert.Equal(1, delete.Database.ExecuteSqlInterpolated(
                $"DELETE FROM ef10conformance_cascadeprincipals WHERE id = {cascade.Id}"));
            Assert.Equal(1, delete.Database.ExecuteSqlInterpolated(
                $"DELETE FROM ef10conformance_setnullprincipals WHERE id = {setNull.Id}"));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.False(verify.Set<CascadePrincipal>().Any(item => item.Id == cascade.Id));
        Assert.False(verify.Set<CascadeDependent>().Any(item => item.PrincipalId == cascade.Id));
        var orphan = verify.Set<SetNullDependent>().AsNoTracking()
            .Single(item => item.Id == setNull.Dependents.Single().Id);
        Assert.Null(orphan.PrincipalId);
    }

    private static async Task<(CascadePrincipal Cascade, SetNullPrincipal SetNull)> SeedAsync()
    {
        var cascade = new CascadePrincipal
        {
            Id = Guid.NewGuid(),
            Dependents = [new CascadeDependent { Id = Guid.NewGuid() }]
        };
        var setNull = new SetNullPrincipal
        {
            Id = Guid.NewGuid(),
            Dependents = [new SetNullDependent { Id = Guid.NewGuid() }]
        };

        await using var seed = ProviderTestRuntime.CreateContext();
        await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
        seed.AddRange(cascade, setNull);
        Assert.Equal(4, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        return (cascade, setNull);
    }
}
