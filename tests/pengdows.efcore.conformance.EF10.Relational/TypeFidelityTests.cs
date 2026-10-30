using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
public sealed class TypeFidelityTests
{
    public static TheoryData<decimal> DecimalBoundaryValues => new()
    {
        0m,
        0.0001m,
        -0.0001m,
        1m,
        -1m,
        123456789.1234m,
        -123456789.1234m,
        99999999999999.9999m,
        -99999999999999.9999m
    };

    [Fact]
    [BehavioralCoverage("EF10-TYPES-ROUNDTRIP-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder.HasPrecision", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder`1.HasConversion", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync")]
    [NativeTypeCoverage("DATE", typeof(DateOnly), "EF10-TYPES-ROUNDTRIP-ASYNC")]
    [NativeTypeCoverage("TIME", typeof(TimeOnly), "EF10-TYPES-ROUNDTRIP-ASYNC")]
    [NativeTypeCoverage("BOOLEAN", typeof(bool), "EF10-TYPES-ROUNDTRIP-ASYNC")]
    [NativeTypeCoverage("BLOB", typeof(byte[]), "EF10-TYPES-ROUNDTRIP-ASYNC")]
    [NativeTypeCoverage("TEXT", typeof(TestMood), "EF10-TYPES-ROUNDTRIP-ASYNC")]
    [NativeTypeCoverage("VARCHAR(8)", typeof(TestMood), "EF10-TYPES-ROUNDTRIP-ASYNC")]
    [NativeTypeCoverage("UUID", typeof(Guid), "EF10-TYPES-ROUNDTRIP-ASYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("NUMERIC", typeof(decimal), "EF10-TYPES-ROUNDTRIP-ASYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("TIME WITHOUT TIME ZONE", typeof(TimeOnly), "EF10-TYPES-ROUNDTRIP-ASYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("BYTEA", typeof(byte[]), "EF10-TYPES-ROUNDTRIP-ASYNC", "Npgsql EF Core PostgreSQL")]
    public async Task CommonClrValues_RoundTripThroughFreshContext()
    {
        var expected = CreateValue(123456789.1234m);

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

        var matchingId = await read.ScalarRecords.AsNoTracking()
            .Where(record => record.Id == expected.Id &&
                             record.Day == expected.Day &&
                             record.Start == expected.Start &&
                             record.Enabled == expected.Enabled &&
                             record.Mood == expected.Mood)
            .Select(record => record.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected.Id, matchingId);
    }

    [Fact]
    [BehavioralCoverage("EF10-TYPES-ROUNDTRIP-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder.HasPrecision", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder`1.HasConversion", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges")]
    [NativeTypeCoverage("DATE", typeof(DateOnly), "EF10-TYPES-ROUNDTRIP-SYNC")]
    [NativeTypeCoverage("TIME", typeof(TimeOnly), "EF10-TYPES-ROUNDTRIP-SYNC")]
    [NativeTypeCoverage("BOOLEAN", typeof(bool), "EF10-TYPES-ROUNDTRIP-SYNC")]
    [NativeTypeCoverage("BLOB", typeof(byte[]), "EF10-TYPES-ROUNDTRIP-SYNC")]
    [NativeTypeCoverage("TEXT", typeof(TestMood), "EF10-TYPES-ROUNDTRIP-SYNC")]
    [NativeTypeCoverage("VARCHAR(8)", typeof(TestMood), "EF10-TYPES-ROUNDTRIP-SYNC")]
    [NativeTypeCoverage("UUID", typeof(Guid), "EF10-TYPES-ROUNDTRIP-SYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("NUMERIC", typeof(decimal), "EF10-TYPES-ROUNDTRIP-SYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("TIME WITHOUT TIME ZONE", typeof(TimeOnly), "EF10-TYPES-ROUNDTRIP-SYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("BYTEA", typeof(byte[]), "EF10-TYPES-ROUNDTRIP-SYNC", "Npgsql EF Core PostgreSQL")]
    public async Task CommonClrValues_RoundTripThroughFreshContextSynchronously()
    {
        var expected = CreateValue(123456789.1234m);

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.ScalarRecords.Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = read.ScalarRecords.AsNoTracking().Single(record => record.Id == expected.Id);
        AssertValue(expected, actual);

        var matchingId = read.ScalarRecords.AsNoTracking()
            .Where(record => record.Id == expected.Id &&
                             record.Day == expected.Day &&
                             record.Start == expected.Start &&
                             record.Enabled == expected.Enabled &&
                             record.Mood == expected.Mood)
            .Select(record => record.Id)
            .Single();
        Assert.Equal(expected.Id, matchingId);
    }

    [Fact]
    [BehavioralCoverage("EF10-TYPES-BOUNDARIES-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    [NativeTypeCoverage("DATE", typeof(DateOnly), "EF10-TYPES-BOUNDARIES-ASYNC")]
    [NativeTypeCoverage("TIME", typeof(TimeOnly), "EF10-TYPES-BOUNDARIES-ASYNC")]
    [NativeTypeCoverage("BOOLEAN", typeof(bool), "EF10-TYPES-BOUNDARIES-ASYNC")]
    [NativeTypeCoverage("BLOB", typeof(byte[]), "EF10-TYPES-BOUNDARIES-ASYNC")]
    [NativeTypeCoverage("TEXT", typeof(TestMood), "EF10-TYPES-BOUNDARIES-ASYNC")]
    [NativeTypeCoverage("UUID", typeof(Guid), "EF10-TYPES-BOUNDARIES-ASYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("NUMERIC", typeof(decimal), "EF10-TYPES-BOUNDARIES-ASYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("TIME WITHOUT TIME ZONE", typeof(TimeOnly), "EF10-TYPES-BOUNDARIES-ASYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("BYTEA", typeof(byte[]), "EF10-TYPES-BOUNDARIES-ASYNC", "Npgsql EF Core PostgreSQL")]
    public async Task CommonClrBoundaryValues_RoundTripThroughFreshContextAsynchronously()
    {
        var expected = new ScalarRecord
        {
            Id = Guid.NewGuid(),
            Amount = 1m,
            Day = DateOnly.MaxValue,
            Start = TimeOnly.MaxValue.Add(TimeSpan.FromTicks(-9)),
            Enabled = false,
            Payload = [],
            Mood = TestMood.Calm
        };

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

        var matchingId = await read.ScalarRecords.AsNoTracking()
            .Where(record => record.Id == expected.Id &&
                             record.Day == expected.Day &&
                             record.Start == expected.Start &&
                             record.Enabled == expected.Enabled &&
                             record.Mood == expected.Mood)
            .Select(record => record.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(expected.Id, matchingId);
    }

    [Fact]
    [BehavioralCoverage("EF10-TYPES-BOUNDARIES-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    [NativeTypeCoverage("DATE", typeof(DateOnly), "EF10-TYPES-BOUNDARIES-SYNC")]
    [NativeTypeCoverage("TIME", typeof(TimeOnly), "EF10-TYPES-BOUNDARIES-SYNC")]
    [NativeTypeCoverage("BOOLEAN", typeof(bool), "EF10-TYPES-BOUNDARIES-SYNC")]
    [NativeTypeCoverage("BLOB", typeof(byte[]), "EF10-TYPES-BOUNDARIES-SYNC")]
    [NativeTypeCoverage("TEXT", typeof(TestMood), "EF10-TYPES-BOUNDARIES-SYNC")]
    [NativeTypeCoverage("UUID", typeof(Guid), "EF10-TYPES-BOUNDARIES-SYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("NUMERIC", typeof(decimal), "EF10-TYPES-BOUNDARIES-SYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("TIME WITHOUT TIME ZONE", typeof(TimeOnly), "EF10-TYPES-BOUNDARIES-SYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("BYTEA", typeof(byte[]), "EF10-TYPES-BOUNDARIES-SYNC", "Npgsql EF Core PostgreSQL")]
    public async Task CommonClrBoundaryValues_RoundTripThroughFreshContextSynchronously()
    {
        var expected = new ScalarRecord
        {
            Id = Guid.NewGuid(),
            Amount = -1m,
            Day = DateOnly.MinValue,
            Start = TimeOnly.MinValue,
            Enabled = false,
            Payload = [],
            Mood = TestMood.Calm
        };

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.ScalarRecords.Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = read.ScalarRecords.AsNoTracking().Single(record => record.Id == expected.Id);
        AssertValue(expected, actual);

        var matchingId = read.ScalarRecords.AsNoTracking()
            .Where(record => record.Id == expected.Id &&
                             record.Day == expected.Day &&
                             record.Start == expected.Start &&
                             record.Enabled == expected.Enabled &&
                             record.Mood == expected.Mood)
            .Select(record => record.Id)
            .Single();
        Assert.Equal(expected.Id, matchingId);
    }

    [Fact]
    [BehavioralCoverage("EF10-TYPES-ADDITIONAL-SCALARS-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    [NativeTypeCoverage("INTEGER", typeof(byte), "EF10-TYPES-ADDITIONAL-SCALARS-ASYNC")]
    [NativeTypeCoverage("INTEGER", typeof(short), "EF10-TYPES-ADDITIONAL-SCALARS-ASYNC")]
    [NativeTypeCoverage("INTEGER", typeof(long), "EF10-TYPES-ADDITIONAL-SCALARS-ASYNC")]
    [NativeTypeCoverage("REAL", typeof(float), "EF10-TYPES-ADDITIONAL-SCALARS-ASYNC")]
    [NativeTypeCoverage("DOUBLE PRECISION", typeof(double), "EF10-TYPES-ADDITIONAL-SCALARS-ASYNC")]
    [NativeTypeCoverage("VARCHAR(1)", typeof(char), "EF10-TYPES-ADDITIONAL-SCALARS-ASYNC")]
    [NativeTypeCoverage("INTERVAL", typeof(TimeSpan), "EF10-TYPES-ADDITIONAL-SCALARS-ASYNC")]
    [NativeTypeCoverage("SMALLINT", typeof(short), "EF10-TYPES-ADDITIONAL-SCALARS-ASYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("BIGINT", typeof(long), "EF10-TYPES-ADDITIONAL-SCALARS-ASYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("DOUBLE PRECISION", typeof(double), "EF10-TYPES-ADDITIONAL-SCALARS-ASYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("CHARACTER", typeof(char), "EF10-TYPES-ADDITIONAL-SCALARS-ASYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("INTERVAL", typeof(TimeSpan), "EF10-TYPES-ADDITIONAL-SCALARS-ASYNC", "Npgsql EF Core PostgreSQL")]
    public async Task AdditionalClrScalars_RoundTripThroughFreshContextAsynchronously()
    {
        var expected = CreateAdditionalValues();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.AdditionalClrTypesRecords.Add(expected);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = await read.AdditionalClrTypesRecords.AsNoTracking()
            .SingleAsync(record => record.Id == expected.Id, TestContext.Current.CancellationToken);
        AssertAdditionalValues(expected, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-TYPES-ADDITIONAL-SCALARS-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    [NativeTypeCoverage("INTEGER", typeof(byte), "EF10-TYPES-ADDITIONAL-SCALARS-SYNC")]
    [NativeTypeCoverage("INTEGER", typeof(short), "EF10-TYPES-ADDITIONAL-SCALARS-SYNC")]
    [NativeTypeCoverage("INTEGER", typeof(long), "EF10-TYPES-ADDITIONAL-SCALARS-SYNC")]
    [NativeTypeCoverage("REAL", typeof(float), "EF10-TYPES-ADDITIONAL-SCALARS-SYNC")]
    [NativeTypeCoverage("DOUBLE PRECISION", typeof(double), "EF10-TYPES-ADDITIONAL-SCALARS-SYNC")]
    [NativeTypeCoverage("VARCHAR(1)", typeof(char), "EF10-TYPES-ADDITIONAL-SCALARS-SYNC")]
    [NativeTypeCoverage("INTERVAL", typeof(TimeSpan), "EF10-TYPES-ADDITIONAL-SCALARS-SYNC")]
    [NativeTypeCoverage("SMALLINT", typeof(short), "EF10-TYPES-ADDITIONAL-SCALARS-SYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("BIGINT", typeof(long), "EF10-TYPES-ADDITIONAL-SCALARS-SYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("DOUBLE PRECISION", typeof(double), "EF10-TYPES-ADDITIONAL-SCALARS-SYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("CHARACTER", typeof(char), "EF10-TYPES-ADDITIONAL-SCALARS-SYNC", "Npgsql EF Core PostgreSQL")]
    [NativeTypeCoverage("INTERVAL", typeof(TimeSpan), "EF10-TYPES-ADDITIONAL-SCALARS-SYNC", "Npgsql EF Core PostgreSQL")]
    public async Task AdditionalClrScalars_RoundTripThroughFreshContextSynchronously()
    {
        var expected = CreateAdditionalValues();
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.AdditionalClrTypesRecords.Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = read.AdditionalClrTypesRecords.AsNoTracking().Single(record => record.Id == expected.Id);
        AssertAdditionalValues(expected, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-TYPES-ADDITIONAL-INTEGER-BOUNDARIES-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task AdditionalClrIntegerBoundaryValues_RoundTripAsynchronously()
    {
        var expected = CreateAdditionalValues();
        expected.ByteValue = byte.MinValue;
        expected.Int16Value = short.MaxValue;
        expected.Int64Value = long.MaxValue;

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.AdditionalClrTypesRecords.Add(expected);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = await read.AdditionalClrTypesRecords.AsNoTracking()
            .SingleAsync(record => record.Id == expected.Id, TestContext.Current.CancellationToken);
        AssertAdditionalValues(expected, actual);
    }

    [Fact]
    [BehavioralCoverage("EF10-TYPES-ADDITIONAL-INTEGER-BOUNDARIES-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    public async Task AdditionalClrIntegerBoundaryValues_RoundTripSynchronously()
    {
        var expected = CreateAdditionalValues();
        expected.ByteValue = byte.MinValue;
        expected.Int16Value = short.MaxValue;
        expected.Int64Value = long.MaxValue;

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.AdditionalClrTypesRecords.Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = read.AdditionalClrTypesRecords.AsNoTracking().Single(record => record.Id == expected.Id);
        AssertAdditionalValues(expected, actual);
    }

    [Theory]
    [MemberData(nameof(DecimalBoundaryValues))]
    [BehavioralCoverage("EF10-TYPES-DECIMAL-BOUNDARIES-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder.HasPrecision", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task Decimal18Scale4_RoundTripsBoundaryValuesAsynchronously(decimal amount)
    {
        var expected = CreateValue(amount);
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.ScalarRecords.Add(expected);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = await read.ScalarRecords.AsNoTracking().SingleAsync(
            record => record.Id == expected.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(amount, actual.Amount);
    }

    [Theory]
    [MemberData(nameof(DecimalBoundaryValues))]
    [BehavioralCoverage("EF10-TYPES-DECIMAL-BOUNDARIES-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder.HasPrecision", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    public async Task Decimal18Scale4_RoundTripsBoundaryValuesSynchronously(decimal amount)
    {
        var expected = CreateValue(amount);
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.ScalarRecords.Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = read.ScalarRecords.AsNoTracking().Single(record => record.Id == expected.Id);
        Assert.Equal(amount, actual.Amount);
    }

    [Theory]
    [MemberData(nameof(DecimalBoundaryValues))]
    [BehavioralCoverage("EF10-TYPES-DECIMAL-CONVERSION-ASYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder`1.HasConversion", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    [NativeTypeCoverage("TEXT", typeof(decimal), "EF10-TYPES-DECIMAL-CONVERSION-ASYNC")]
    public async Task DecimalStringConversion_PreservesBoundaryValuesAsynchronously(decimal amount)
    {
        var expected = CreateValue(0m);
        expected.ConvertedAmount = amount;

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.ScalarRecords.Add(expected);
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = await read.ScalarRecords.AsNoTracking().SingleAsync(
            record => record.Id == expected.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(amount, actual.ConvertedAmount);
        Assert.Equal(expected.Id, await read.ScalarRecords.AsNoTracking()
            .Where(record => record.Id == expected.Id && record.ConvertedAmount == amount)
            .Select(record => record.Id)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(DecimalBoundaryValues))]
    [BehavioralCoverage("EF10-TYPES-DECIMAL-CONVERSION-SYNC", "Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder`1.HasConversion", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    [NativeTypeCoverage("TEXT", typeof(decimal), "EF10-TYPES-DECIMAL-CONVERSION-SYNC")]
    public async Task DecimalStringConversion_PreservesBoundaryValuesSynchronously(decimal amount)
    {
        var expected = CreateValue(0m);
        expected.ConvertedAmount = amount;

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.ScalarRecords.Add(expected);
            Assert.Equal(1, write.SaveChanges());
        }

        await using var read = ProviderTestRuntime.CreateContext();
        var actual = read.ScalarRecords.AsNoTracking().Single(record => record.Id == expected.Id);
        Assert.Equal(amount, actual.ConvertedAmount);
        Assert.Equal(expected.Id, read.ScalarRecords.AsNoTracking()
            .Where(record => record.Id == expected.Id && record.ConvertedAmount == amount)
            .Select(record => record.Id)
            .Single());
    }

    [Fact]
    [BehavioralCoverage("EF10-TYPES-NULLABLE-ROUNDTRIP-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry`1.Property")]
    public async Task NullableClrValues_RoundTripAndUpdateThroughFreshContextsAsynchronously()
    {
        var expected = CreateNullableValue();
        var nullAtInsert = new NullableScalarRecord { Id = Guid.NewGuid() };
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.NullableScalarRecords.AddRange(expected, nullAtInsert);
            Assert.Equal(2, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = await update.NullableScalarRecords.SingleAsync(
                record => record.Id == expected.Id,
                TestContext.Current.CancellationToken);
            AssertNullableValue(expected, tracked);
            tracked.Integer = null;
            tracked.Flag = null;
            tracked.Amount = null;
            tracked.Text = null;
            tracked.Day = null;
            tracked.Time = null;
            tracked.Payload = null;
            tracked.Token = null;
            tracked.Duration = null;
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.NullableScalarRecords.AsNoTracking().SingleAsync(
            record => record.Id == expected.Id,
            TestContext.Current.CancellationToken);
        Assert.All(GetNullableValues(actual), value => Assert.Null(value));
        var insertedNull = await verify.NullableScalarRecords.AsNoTracking().SingleAsync(
            record => record.Id == nullAtInsert.Id,
            TestContext.Current.CancellationToken);
        Assert.All(GetNullableValues(insertedNull), value => Assert.Null(value));
    }

    [Fact]
    [BehavioralCoverage("EF10-TYPES-NULLABLE-ROUNDTRIP-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single", "Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry`1.Property")]
    public async Task NullableClrValues_RoundTripAndUpdateThroughFreshContextsSynchronously()
    {
        var expected = CreateNullableValue();
        var nullAtInsert = new NullableScalarRecord { Id = Guid.NewGuid() };
        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.NullableScalarRecords.AddRange(expected, nullAtInsert);
            Assert.Equal(2, write.SaveChanges());
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var tracked = update.NullableScalarRecords.Single(record => record.Id == expected.Id);
            AssertNullableValue(expected, tracked);
            tracked.Integer = null;
            tracked.Flag = null;
            tracked.Amount = null;
            tracked.Text = null;
            tracked.Day = null;
            tracked.Time = null;
            tracked.Payload = null;
            tracked.Token = null;
            tracked.Duration = null;
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.NullableScalarRecords.AsNoTracking().Single(record => record.Id == expected.Id);
        Assert.All(GetNullableValues(actual), value => Assert.Null(value));
        var insertedNull = verify.NullableScalarRecords.AsNoTracking().Single(record => record.Id == nullAtInsert.Id);
        Assert.All(GetNullableValues(insertedNull), value => Assert.Null(value));
    }

    private static ScalarRecord CreateValue(decimal amount) => new()
    {
        Id = Guid.NewGuid(),
        Amount = amount,
        Day = new DateOnly(2024, 2, 29),
        Start = new TimeOnly(13, 14, 15),
        Enabled = true,
        Payload = [0, 1, 127, 255],
        Mood = TestMood.Curious
    };

    private static NullableScalarRecord CreateNullableValue() => new()
    {
        Id = Guid.NewGuid(),
        Integer = 42,
        Flag = true,
        Amount = 123456789.1234m,
        Text = "nullable value",
        Day = new DateOnly(2024, 2, 29),
        Time = new TimeOnly(13, 14, 15),
        Payload = [0, 1, 127, 255],
        Token = Guid.NewGuid(),
        Duration = TimeSpan.FromTicks(9876543210)
    };

    private static IEnumerable<object?> GetNullableValues(NullableScalarRecord value)
    {
        yield return value.Integer;
        yield return value.Flag;
        yield return value.Amount;
        yield return value.Text;
        yield return value.Day;
        yield return value.Time;
        yield return value.Payload;
        yield return value.Token;
        yield return value.Duration;
    }

    private static void AssertNullableValue(NullableScalarRecord expected, NullableScalarRecord actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Integer, actual.Integer);
        Assert.Equal(expected.Flag, actual.Flag);
        Assert.Equal(expected.Amount, actual.Amount);
        Assert.Equal(expected.Text, actual.Text);
        Assert.Equal(expected.Day, actual.Day);
        Assert.Equal(expected.Time, actual.Time);
        Assert.Equal(expected.Payload, actual.Payload);
        Assert.Equal(expected.Token, actual.Token);
        Assert.Equal(expected.Duration, actual.Duration);
    }

    private static AdditionalClrTypesRecord CreateAdditionalValues() => new()
    {
        Id = Guid.NewGuid(),
        ByteValue = byte.MaxValue,
        Int16Value = short.MinValue,
        Int64Value = long.MinValue,
        SingleValue = 1234.5f,
        DoubleValue = 9876.125d,
        CharacterValue = 'Ω',
        DurationValue = TimeSpan.FromTicks(9876543210)
    };

    private static void AssertAdditionalValues(AdditionalClrTypesRecord expected, AdditionalClrTypesRecord actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.ByteValue, actual.ByteValue);
        Assert.Equal(expected.Int16Value, actual.Int16Value);
        Assert.Equal(expected.Int64Value, actual.Int64Value);
        Assert.Equal(expected.SingleValue, actual.SingleValue);
        Assert.Equal(expected.DoubleValue, actual.DoubleValue);
        Assert.Equal(expected.CharacterValue, actual.CharacterValue);
        Assert.Equal(expected.DurationValue, actual.DurationValue);
    }

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
