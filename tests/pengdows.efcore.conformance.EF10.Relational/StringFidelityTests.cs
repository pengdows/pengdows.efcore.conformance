using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class StringFidelityTests
{
    private const string UnicodeText = "Cafe\u0301 / Ångström / 雪 / 🛰️";
    private const string AdversarialSuffix = "[%_]'\\ emoji 👩🏽‍💻 e\u0301 NUL:\0 trailing  ";

    [Fact]
    [BehavioralCoverage("EF10-STRING-LOB-RAW-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "System.Data.Common.DbCommand.ExecuteScalarAsync")]
    public async Task LargeUnicodeText_RoundTripsThroughIndependentAdoNetChannelAsynchronously()
    {
        var prefix = Guid.NewGuid().ToString("N");
        var expected = prefix + string.Concat(Enumerable.Repeat("🚀e\u0301/雪%", 20_000));
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.TextFidelityRecords.Add(new TextFidelityRecord { Id = Guid.NewGuid(), Text = expected });
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var connection = ProviderTestRuntime.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT large_text FROM ef10conformancelargetext WHERE large_text LIKE '{prefix}%'";
        Assert.Equal(expected, Assert.IsType<string>(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)));
    }

    [Fact]
    [BehavioralCoverage("EF10-STRING-LOB-RAW-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Data.Common.DbCommand.ExecuteScalar")]
    public async Task LargeUnicodeText_RoundTripsThroughIndependentAdoNetChannelSynchronously()
    {
        var prefix = Guid.NewGuid().ToString("N");
        var expected = prefix + string.Concat(Enumerable.Repeat("🚀e\u0301/雪%", 20_000));
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.TextFidelityRecords.Add(new TextFidelityRecord { Id = Guid.NewGuid(), Text = expected });
            Assert.Equal(1, write.SaveChanges());
        }

        await using var connection = ProviderTestRuntime.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT large_text FROM ef10conformancelargetext WHERE large_text LIKE '{prefix}%'";
        Assert.Equal(expected, Assert.IsType<string>(command.ExecuteScalar()));
    }

    [Fact]
    [BehavioralCoverage("EF10-STRING-ADVERSARIAL-RAW-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "System.Data.Common.DbConnection.CreateCommand", "System.Data.Common.DbCommand.ExecuteScalarAsync")]
    public async Task AdversarialString_RoundTripsExactlyThroughIndependentAdoNetChannelAsynchronously()
    {
        var id = Guid.NewGuid();
        var batch = Guid.NewGuid().ToString("N");
        var expected = batch + AdversarialSuffix;
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.NullableScalarRecords.Add(new NullableScalarRecord { Id = id, Text = expected });
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var connection = ProviderTestRuntime.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT text_value FROM ef10conformancenullablescalars WHERE text_value LIKE '{batch}%'";
        var actual = Assert.IsType<string>(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));

        Assert.Equal(expected, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-STRING-ADVERSARIAL-RAW-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Data.Common.DbConnection.CreateCommand", "System.Data.Common.DbCommand.ExecuteScalar")]
    public async Task AdversarialString_RoundTripsExactlyThroughIndependentAdoNetChannelSynchronously()
    {
        var id = Guid.NewGuid();
        var batch = Guid.NewGuid().ToString("N");
        var expected = batch + AdversarialSuffix;
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.NullableScalarRecords.Add(new NullableScalarRecord { Id = id, Text = expected });
            Assert.Equal(1, write.SaveChanges());
        }

        await using var connection = ProviderTestRuntime.CreateConnection();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT text_value FROM ef10conformancenullablescalars WHERE text_value LIKE '{batch}%'";
        var actual = Assert.IsType<string>(command.ExecuteScalar());

        Assert.Equal(expected, actual);
    }

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
