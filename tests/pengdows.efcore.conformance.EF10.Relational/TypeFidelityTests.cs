using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class TypeFidelityTests
{
    [Fact]
    [BehavioralCoverage("EF10-TYPES-ROUNDTRIP-ASYNC", "Microsoft.EntityFrameworkCore.RelationalPropertyBuilderExtensions.HasColumnType", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder.HasPrecision", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder`1.HasConversion", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    public async Task CommonClrValues_RoundTripThroughFreshContext()
    {
        var expected = CreateValue();

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.ScalarRecords.Add(expected);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = await read.ScalarRecords.AsNoTracking()
            .SingleAsync(record => record.Id == expected.Id, TestContext.Current.CancellationToken);
        AssertValue(expected, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-TYPES-ROUNDTRIP-SYNC", "Microsoft.EntityFrameworkCore.RelationalPropertyBuilderExtensions.HasColumnType", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder`1.HasConversion", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    public async Task CommonClrValues_RoundTripThroughFreshContextSynchronously()
    {
        var expected = CreateValue();

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.ScalarRecords.Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = read.ScalarRecords.AsNoTracking().Single(record => record.Id == expected.Id);
        AssertValue(expected, actual);
    }

    private static ScalarRecord CreateValue() => new()
    {
        Id = Guid.NewGuid(),
        Amount = 123456789.1234m,
        Day = new DateOnly(2024, 2, 29),
        Start = new TimeOnly(13, 14, 15),
        Enabled = true,
        Payload = [0, 1, 127, 255],
        Mood = TestMood.Curious
    };

    private static void AssertValue(ScalarRecord expected, ScalarRecord actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Amount, actual.Amount);
        Assert.Equal(expected.Day, actual.Day);
        Assert.Equal(expected.Start, actual.Start);
        Assert.Equal(expected.Enabled, actual.Enabled);
        Assert.Equal(expected.Payload, actual.Payload);
        Assert.Equal(expected.Mood, actual.Mood);
    }
}
