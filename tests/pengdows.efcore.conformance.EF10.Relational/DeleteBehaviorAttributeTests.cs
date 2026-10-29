using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("relational")]
public sealed class DeleteBehaviorAttributeTests
{
    [Fact]
    [BehavioralCoverage("EF10-DELETE-BEHAVIOR-RESTRICT-ASYNC", "Microsoft.EntityFrameworkCore.DeleteBehaviorAttribute", "Microsoft.EntityFrameworkCore.DeleteBehaviorAttribute..ctor", "Microsoft.EntityFrameworkCore.DeleteBehaviorAttribute.Behavior", "Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolatedAsync")]
    public async Task DeleteBehaviorRestrictAttribute_PreventsDeletingPrincipalWithDependentsAsync()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var principal = new RestrictPrincipal
        {
            Id = Guid.NewGuid(),
            Dependents = [new RestrictDependent { Id = Guid.NewGuid() }]
        };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.Set<RestrictPrincipal>().Add(principal);
            Assert.Equal(2, await seed.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            await Assert.ThrowsAnyAsync<DbException>(() => delete.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM ef10conformance_restrictprincipals WHERE id = {principal.Id}",
                TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(await verify.Set<RestrictPrincipal>().AnyAsync(
            item => item.Id == principal.Id,
            TestContext.Current.CancellationToken));
        Assert.True(await verify.Set<RestrictDependent>().AnyAsync(
            item => item.PrincipalId == principal.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    [BehavioralCoverage("EF10-DELETE-BEHAVIOR-RESTRICT-SYNC", "Microsoft.EntityFrameworkCore.DeleteBehaviorAttribute", "Microsoft.EntityFrameworkCore.DeleteBehaviorAttribute..ctor", "Microsoft.EntityFrameworkCore.DeleteBehaviorAttribute.Behavior", "Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.ExecuteSqlInterpolated")]
    public async Task DeleteBehaviorRestrictAttribute_PreventsDeletingPrincipalWithDependentsSynchronously()
    {
        ProviderTestRuntime.RequireCapability("relational");
        var principal = new RestrictPrincipal
        {
            Id = Guid.NewGuid(),
            Dependents = [new RestrictDependent { Id = Guid.NewGuid() }]
        };

        await using (var seed = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(seed, TestContext.Current.CancellationToken);
            seed.Set<RestrictPrincipal>().Add(principal);
            Assert.Equal(2, seed.SaveChanges());
        }

        await using (var delete = ProviderTestRuntime.CreateContext())
        {
            Assert.ThrowsAny<DbException>(() => delete.Database.ExecuteSqlInterpolated(
                $"DELETE FROM ef10conformance_restrictprincipals WHERE id = {principal.Id}"));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        Assert.True(verify.Set<RestrictPrincipal>().Any(item => item.Id == principal.Id));
        Assert.True(verify.Set<RestrictDependent>().Any(item => item.PrincipalId == principal.Id));
    }
}
