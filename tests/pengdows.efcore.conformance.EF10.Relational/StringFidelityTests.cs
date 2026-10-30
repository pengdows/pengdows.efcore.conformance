using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class StringFidelityTests
{
    private const string UnicodeText = "Cafe\u0301 / Ångström / 雪 / 🛰️";

    [Fact]
    [BehavioralCoverage("EF10-STRING-FIDELITY-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "System.Linq.Queryable.Where", "System.Linq.Queryable.Select", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task StringValues_RoundTripAndCompareUnicodeAsynchronously()
    {
        var emptyId = Guid.NewGuid();
        var unicodeId = Guid.NewGuid();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.NullableScalarRecords.AddRange(
                new NullableScalarRecord { Id = emptyId, Text = string.Empty },
                new NullableScalarRecord { Id = unicodeId, Text = UnicodeText });
            Assert.Equal(2, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var empty = await verify.NullableScalarRecords.AsNoTracking()
            .Where(record => record.Id == emptyId)
            .Select(record => record.Text)
            .SingleAsync(TestContext.Current.CancellationToken);
        var unicode = await verify.NullableScalarRecords.AsNoTracking()
            .Where(record => record.Id == unicodeId)
            .Select(record => record.Text)
            .SingleAsync(TestContext.Current.CancellationToken);
        var matchedId = await verify.NullableScalarRecords.AsNoTracking()
            .Where(record => record.Id == unicodeId && record.Text == UnicodeText)
            .Select(record => record.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(string.Empty, empty);
        Assert.Equal(UnicodeText, unicode);
        Assert.Equal(unicodeId, matchedId);
    }

    [Fact]
    [BehavioralCoverage("EF10-STRING-FIDELITY-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Where", "System.Linq.Queryable.Select", "System.Linq.Queryable.Single")]
    public async Task StringValues_RoundTripAndCompareUnicodeSynchronously()
    {
        var emptyId = Guid.NewGuid();
        var unicodeId = Guid.NewGuid();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.NullableScalarRecords.AddRange(
                new NullableScalarRecord { Id = emptyId, Text = string.Empty },
                new NullableScalarRecord { Id = unicodeId, Text = UnicodeText });
            Assert.Equal(2, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var empty = verify.NullableScalarRecords.AsNoTracking()
            .Where(record => record.Id == emptyId)
            .Select(record => record.Text)
            .Single();
        var unicode = verify.NullableScalarRecords.AsNoTracking()
            .Where(record => record.Id == unicodeId)
            .Select(record => record.Text)
            .Single();
        var matchedId = verify.NullableScalarRecords.AsNoTracking()
            .Where(record => record.Id == unicodeId && record.Text == UnicodeText)
            .Select(record => record.Id)
            .Single();

        Assert.Equal(string.Empty, empty);
        Assert.Equal(UnicodeText, unicode);
        Assert.Equal(unicodeId, matchedId);
    }
}
