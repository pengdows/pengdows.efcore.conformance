using System.Collections;
using System.Linq.Expressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;
using Xunit;

namespace pengdows.efcore.conformance.EF10.Relational;

[Collection(RelationalTestCollection.Name)]
[CapabilityRequirement("native-type-fidelity")]
public sealed class DiscoveredNativeTypeTests
{
    public static TheoryData<int, string, string> DiscoveredMappings
    {
        get
        {
            var data = new TheoryData<int, string, string>();
            foreach (var probe in ProviderTestRuntime.NativeTypeProbes)
            {
                data.Add(probe.Index, probe.Mapping.DatabaseTypeName, probe.Mapping.ClrType.Name);
            }

            if (data.Count == 0)
            {
                data.Add(-1, "NO-INDEPENDENT-TYPE-PROFILE", "none");
            }

            return data;
        }
    }

    public static TheoryData<int, string, string> EqualityMappings
    {
        get
        {
            var data = new TheoryData<int, string, string>();
            foreach (var probe in ProviderTestRuntime.NativeTypeProbes.Where(probe => IsEqualityApplicable(probe.Mapping)))
            {
                data.Add(probe.Index, probe.Mapping.DatabaseTypeName, probe.Mapping.ClrType.Name);
            }

            if (data.Count == 0)
            {
                data.Add(-1, "NO-SCALAR-EQUALITY-MAPPINGS", "none");
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(DiscoveredMappings))]
    [BehavioralCoverage("EF10-NATIVE-TYPE-ROUNDTRIP-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task DiscoveredNativeType_RoundTripsAsynchronously(int probeIndex, string databaseTypeName, string clrTypeName)
    {
        ProviderTestRuntime.RequireCapability("native-type-fidelity");
        var probe = GetProbe(probeIndex, databaseTypeName, clrTypeName);
        var id = Guid.NewGuid();
        var expected = NativeTypeSampleFactory.Create(probe.Mapping);

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.Set<Dictionary<string, object>>(probe.EntityTypeName).Add(CreateRow(id, expected));
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.Set<Dictionary<string, object>>(probe.EntityTypeName)
            .AsNoTracking()
            .SingleAsync(row => EF.Property<Guid>(row, "id") == id, TestContext.Current.CancellationToken);
        AssertValueEqual(expected, actual["value"], probe.Mapping);
    }

    [Theory]
    [MemberData(nameof(DiscoveredMappings))]
    [BehavioralCoverage("EF10-NATIVE-TYPE-ROUNDTRIP-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    public async Task DiscoveredNativeType_RoundTripsSynchronously(int probeIndex, string databaseTypeName, string clrTypeName)
    {
        ProviderTestRuntime.RequireCapability("native-type-fidelity");
        var probe = GetProbe(probeIndex, databaseTypeName, clrTypeName);
        var id = Guid.NewGuid();
        var expected = NativeTypeSampleFactory.Create(probe.Mapping);

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.Set<Dictionary<string, object>>(probe.EntityTypeName).Add(CreateRow(id, expected));
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.Set<Dictionary<string, object>>(probe.EntityTypeName)
            .AsNoTracking()
            .Single(row => EF.Property<Guid>(row, "id") == id);
        AssertValueEqual(expected, actual["value"], probe.Mapping);
    }

    [Theory]
    [MemberData(nameof(DiscoveredMappings))]
    [BehavioralCoverage("EF10-NATIVE-TYPE-UPDATE-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task DiscoveredNativeType_UpdatesAndRoundTripsAsynchronously(int probeIndex, string databaseTypeName, string clrTypeName)
    {
        ProviderTestRuntime.RequireCapability("native-type-fidelity");
        var probe = GetProbe(probeIndex, databaseTypeName, clrTypeName);
        var id = Guid.NewGuid();
        var expected = NativeTypeSampleFactory.CreateAlternate(probe.Mapping);

        await using (var insert = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(insert, TestContext.Current.CancellationToken);
            insert.Set<Dictionary<string, object>>(probe.EntityTypeName).Add(CreateRow(id, NativeTypeSampleFactory.Create(probe.Mapping)));
            Assert.Equal(1, await insert.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var row = await update.Set<Dictionary<string, object>>(probe.EntityTypeName)
                .SingleAsync(item => EF.Property<Guid>(item, "id") == id, TestContext.Current.CancellationToken);
            row["value"] = expected;
            Assert.Equal(1, await update.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.Set<Dictionary<string, object>>(probe.EntityTypeName)
            .AsNoTracking()
            .SingleAsync(row => EF.Property<Guid>(row, "id") == id, TestContext.Current.CancellationToken);
        AssertValueEqual(expected, actual["value"], probe.Mapping);
    }

    [Theory]
    [MemberData(nameof(DiscoveredMappings))]
    [BehavioralCoverage("EF10-NATIVE-TYPE-UPDATE-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    public async Task DiscoveredNativeType_UpdatesAndRoundTripsSynchronously(int probeIndex, string databaseTypeName, string clrTypeName)
    {
        ProviderTestRuntime.RequireCapability("native-type-fidelity");
        var probe = GetProbe(probeIndex, databaseTypeName, clrTypeName);
        var id = Guid.NewGuid();
        var expected = NativeTypeSampleFactory.CreateAlternate(probe.Mapping);

        await using (var insert = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(insert, TestContext.Current.CancellationToken);
            insert.Set<Dictionary<string, object>>(probe.EntityTypeName).Add(CreateRow(id, NativeTypeSampleFactory.Create(probe.Mapping)));
            Assert.Equal(1, insert.SaveChanges());
        }

        await using (var update = ProviderTestRuntime.CreateContext())
        {
            var row = update.Set<Dictionary<string, object>>(probe.EntityTypeName)
                .Single(item => EF.Property<Guid>(item, "id") == id);
            row["value"] = expected;
            Assert.Equal(1, update.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.Set<Dictionary<string, object>>(probe.EntityTypeName)
            .AsNoTracking()
            .Single(row => EF.Property<Guid>(row, "id") == id);
        AssertValueEqual(expected, actual["value"], probe.Mapping);
    }

    [Theory]
    [MemberData(nameof(DiscoveredMappings))]
    [BehavioralCoverage("EF10-NATIVE-TYPE-NULL-ASYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync")]
    public async Task DiscoveredNativeType_PreservesNullAsynchronously(int probeIndex, string databaseTypeName, string clrTypeName)
    {
        ProviderTestRuntime.RequireCapability("native-type-fidelity");
        var probe = GetProbe(probeIndex, databaseTypeName, clrTypeName);
        var id = Guid.NewGuid();

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.Set<Dictionary<string, object>>(probe.NullableEntityTypeName).Add(CreateRow(id, null!));
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = await verify.Set<Dictionary<string, object>>(probe.NullableEntityTypeName)
            .AsNoTracking()
            .SingleAsync(row => EF.Property<Guid>(row, "id") == id, TestContext.Current.CancellationToken);
        Assert.Null(actual["value"]);
    }

    [Theory]
    [MemberData(nameof(DiscoveredMappings))]
    [BehavioralCoverage("EF10-NATIVE-TYPE-NULL-SYNC", "Microsoft.EntityFrameworkCore.DbContext.SaveChanges", "System.Linq.Queryable.Single")]
    public async Task DiscoveredNativeType_PreservesNullSynchronously(int probeIndex, string databaseTypeName, string clrTypeName)
    {
        ProviderTestRuntime.RequireCapability("native-type-fidelity");
        var probe = GetProbe(probeIndex, databaseTypeName, clrTypeName);
        var id = Guid.NewGuid();

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.Set<Dictionary<string, object>>(probe.NullableEntityTypeName).Add(CreateRow(id, null!));
            Assert.Equal(1, write.SaveChanges());
        }

        await using var verify = ProviderTestRuntime.CreateContext();
        var actual = verify.Set<Dictionary<string, object>>(probe.NullableEntityTypeName)
            .AsNoTracking()
            .Single(row => EF.Property<Guid>(row, "id") == id);
        Assert.Null(actual["value"]);
    }

    [Theory]
    [MemberData(nameof(EqualityMappings))]
    [BehavioralCoverage("EF10-NATIVE-TYPE-PARAMETER-ASYNC", "Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync", "Microsoft.EntityFrameworkCore.EF.Property")]
    public async Task DiscoveredNativeType_QueriesByParameterizedValueAsynchronously(int probeIndex, string databaseTypeName, string clrTypeName)
    {
        ProviderTestRuntime.RequireCapability("native-type-fidelity");
        var probe = GetProbe(probeIndex, databaseTypeName, clrTypeName);
        AssertEqualityApplicable(probe.Mapping);
        var id = Guid.NewGuid();
        var expected = NativeTypeSampleFactory.Create(probe.Mapping);

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.Set<Dictionary<string, object>>(probe.EntityTypeName).Add(CreateRow(id, expected));
            Assert.Equal(1, await write.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var query = ProviderTestRuntime.CreateContext();
        var row = await query.Set<Dictionary<string, object>>(probe.EntityTypeName)
            .Where(BuildValuePredicate(probe.Mapping.ClrType, expected))
            .Where(item => EF.Property<Guid>(item, "id") == id)
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(id, row["id"]);
        AssertValueEqual(expected, row["value"], probe.Mapping);
    }

    [Theory]
    [MemberData(nameof(EqualityMappings))]
    [BehavioralCoverage("EF10-NATIVE-TYPE-PARAMETER-SYNC", "System.Linq.Queryable.Single", "Microsoft.EntityFrameworkCore.EF.Property")]
    public async Task DiscoveredNativeType_QueriesByParameterizedValueSynchronously(int probeIndex, string databaseTypeName, string clrTypeName)
    {
        ProviderTestRuntime.RequireCapability("native-type-fidelity");
        var probe = GetProbe(probeIndex, databaseTypeName, clrTypeName);
        AssertEqualityApplicable(probe.Mapping);
        var id = Guid.NewGuid();
        var expected = NativeTypeSampleFactory.Create(probe.Mapping);

        await using (var write = ProviderTestRuntime.CreateContext())
        {
            await ProviderTestRuntime.InitializeAsync(write, TestContext.Current.CancellationToken);
            write.Set<Dictionary<string, object>>(probe.EntityTypeName).Add(CreateRow(id, expected));
            Assert.Equal(1, write.SaveChanges());
        }

        await using var query = ProviderTestRuntime.CreateContext();
        var row = query.Set<Dictionary<string, object>>(probe.EntityTypeName)
            .Where(BuildValuePredicate(probe.Mapping.ClrType, expected))
            .Where(item => EF.Property<Guid>(item, "id") == id)
            .Single();
        Assert.Equal(id, row["id"]);
        AssertValueEqual(expected, row["value"], probe.Mapping);
    }

    private static NativeTypeProbeDefinition GetProbe(int index, string databaseTypeName, string clrTypeName)
    {
        var probe = ProviderTestRuntime.NativeTypeProbes.SingleOrDefault(item => item.Index == index);
        Assert.NotNull(probe);
        Assert.Equal(databaseTypeName, probe.Mapping.DatabaseTypeName);
        Assert.Equal(clrTypeName, probe.Mapping.ClrType.Name);
        return probe;
    }

    private static Dictionary<string, object> CreateRow(Guid id, object value) => new()
    {
        ["id"] = id,
        ["value"] = value
    };

    private static Expression<Func<Dictionary<string, object>, bool>> BuildValuePredicate(Type valueType, object value)
    {
        var row = Expression.Parameter(typeof(Dictionary<string, object>), "row");
        var property = Expression.Call(
            typeof(EF),
            nameof(EF.Property),
            [valueType],
            row,
            Expression.Constant("value"));
        var equals = Expression.Equal(property, Expression.Constant(value, valueType));
        return Expression.Lambda<Func<Dictionary<string, object>, bool>>(equals, row);
    }

    private static void AssertEqualityApplicable(NativeTypeMapping mapping)
    {
        Assert.True(IsEqualityApplicable(mapping),
            $"Native mapping '{mapping.DatabaseTypeName}' is outside the scalar equality query contract.");
    }

    private static bool IsEqualityApplicable(NativeTypeMapping mapping)
    {
        var type = Nullable.GetUnderlyingType(mapping.ClrType) ?? mapping.ClrType;
        var storeType = mapping.DatabaseTypeName;
        var hasProviderSpecificEquality = storeType.Contains("json", StringComparison.OrdinalIgnoreCase) ||
                                          storeType.Equals("xml", StringComparison.OrdinalIgnoreCase);
        return !type.IsArray && type != typeof(byte[]) && !hasProviderSpecificEquality;
    }

    private static void AssertValueEqual(object expected, object? actual, NativeTypeMapping mapping)
    {
        var equal = expected is string expectedText && actual is string actualText &&
                    mapping.DatabaseTypeName.Contains("json", StringComparison.OrdinalIgnoreCase)
            ? JsonNode.DeepEquals(JsonNode.Parse(expectedText), JsonNode.Parse(actualText))
            : StructuralComparisons.StructuralEqualityComparer.Equals(expected, actual);
        Assert.True(
            equal,
            $"Native type '{mapping.DatabaseTypeName}' mapped from CLR type '{mapping.ClrType.FullName}' did not round-trip the declared sample value.");
    }
}

internal static class NativeTypeSampleFactory
{
    public static object Create(NativeTypeMapping mapping)
    {
        var type = Nullable.GetUnderlyingType(mapping.ClrType) ?? mapping.ClrType;
        if (IsNpgsqlRange(type)) return CreateRange(type, mapping, alternate: false);
        if (type == typeof(byte[])) return new byte[] { 0x4a };
        if (type.IsArray)
        {
            return CreateArray(type.GetElementType()!, mapping);
        }

        if (type == typeof(string) && mapping.DatabaseTypeName.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return "{\"value\":1}";
        }

        if (type == typeof(string) && mapping.DatabaseTypeName.Equals("xml", StringComparison.OrdinalIgnoreCase)) return "<v>1</v>";

        if (type == typeof(bool)) return true;
        if (type == typeof(ulong) && mapping.DatabaseTypeName.Equals("BIT", StringComparison.OrdinalIgnoreCase)) return 1ul;
        if (type == typeof(sbyte)) return sbyte.MinValue;
        if (type == typeof(byte)) return byte.MaxValue;
        if (type == typeof(short)) return short.MinValue;
        if (type == typeof(ushort)) return ushort.MaxValue;
        if (type == typeof(int)) return mapping.DatabaseTypeName.Equals("YEAR", StringComparison.OrdinalIgnoreCase)
            ? 2024
            : mapping.DatabaseTypeName.Equals("MEDIUMINT", StringComparison.OrdinalIgnoreCase) ? -12345 : int.MinValue;
        if (type == typeof(uint)) return mapping.DatabaseTypeName.Equals("MEDIUMINT", StringComparison.OrdinalIgnoreCase)
            ? 12345u
            : uint.MaxValue;
        if (type == typeof(long)) return long.MinValue;
        if (type == typeof(ulong)) return ulong.MaxValue;
        if (type == typeof(decimal)) return mapping.DatabaseTypeName.Equals("money", StringComparison.OrdinalIgnoreCase) ? 12345.67m : 123456789.1234m;
        if (type == typeof(float)) return 1234.5f;
        if (type == typeof(double)) return 9876.125d;
        if (type == typeof(string)) return "A";
        if (type == typeof(Guid)) return Guid.Parse("5d37ddf5-fab9-4e5b-95fb-b5259a5e3d31");
        if (type == typeof(DateOnly)) return new DateOnly(2024, 2, 29);
        if (type == typeof(TimeOnly)) return new TimeOnly(13, 14, 15);
        if (type == typeof(DateTime)) return mapping.DatabaseTypeName.Equals("DATE", StringComparison.OrdinalIgnoreCase)
            ? new DateTime(2024, 2, 29, 0, 0, 0, DateTimeKind.Unspecified)
            : mapping.DatabaseTypeName.Equals("timestamp with time zone", StringComparison.OrdinalIgnoreCase)
                ? new DateTime(2024, 2, 29, 13, 14, 15, DateTimeKind.Utc)
                : new DateTime(2024, 2, 29, 13, 14, 15, DateTimeKind.Unspecified);
        if (type == typeof(DateTimeOffset)) return new DateTimeOffset(2024, 2, 29, 13, 14, 15, TimeSpan.Zero);
        if (type == typeof(IPAddress)) return IPAddress.Parse("192.0.2.1");
        if (type == typeof(IPNetwork)) return IPNetwork.Parse("192.0.2.0/24");
        if (type == typeof(PhysicalAddress)) return PhysicalAddress.Parse(mapping.DatabaseTypeName.Equals("macaddr8", StringComparison.OrdinalIgnoreCase) ? "0011223344556677" : "001122334455");
        if (type == typeof(TimeSpan)) return TimeSpan.FromHours(1) + TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(3);
        if (type == typeof(char)) return 'Q';

        throw new InvalidOperationException($"No centrally defined representative value is available for CLR type '{type.FullName}'.");
    }

    public static object CreateAlternate(NativeTypeMapping mapping)
    {
        var type = Nullable.GetUnderlyingType(mapping.ClrType) ?? mapping.ClrType;
        if (IsNpgsqlRange(type)) return CreateRange(type, mapping, alternate: true);
        if (type == typeof(byte[])) return new byte[] { 0x52 };
        if (type == typeof(int[])) return new[] { 5, 0, 1 };
        if (type.IsArray) return CreateArray(type.GetElementType()!, mapping);
        if (type == typeof(string) && mapping.DatabaseTypeName.Contains("json", StringComparison.OrdinalIgnoreCase)) return "{\"value\":2}";
        if (type == typeof(string) && mapping.DatabaseTypeName.Equals("xml", StringComparison.OrdinalIgnoreCase)) return "<v>2</v>";
        if (type == typeof(bool)) return false;
        if (type == typeof(ulong) && mapping.DatabaseTypeName.Equals("BIT", StringComparison.OrdinalIgnoreCase)) return 0ul;
        if (type == typeof(sbyte)) return sbyte.MaxValue;
        if (type == typeof(byte)) return (byte)0;
        if (type == typeof(short)) return short.MaxValue;
        if (type == typeof(ushort)) return (ushort)0;
        if (type == typeof(int)) return mapping.DatabaseTypeName.Equals("YEAR", StringComparison.OrdinalIgnoreCase)
            ? 2023
            : mapping.DatabaseTypeName.Equals("MEDIUMINT", StringComparison.OrdinalIgnoreCase) ? 12345 : int.MaxValue;
        if (type == typeof(uint)) return 0u;
        if (type == typeof(long)) return long.MaxValue;
        if (type == typeof(ulong)) return 0ul;
        if (type == typeof(decimal)) return mapping.DatabaseTypeName.Equals("money", StringComparison.OrdinalIgnoreCase) ? -987.65m : -987654321.4321m;
        if (type == typeof(float)) return -135.25f;
        if (type == typeof(double)) return -2468.5d;
        if (type == typeof(string)) return "B";
        if (type == typeof(Guid)) return Guid.Parse("84c5903f-330f-42d1-92f4-94ab241b1b32");
        if (type == typeof(DateOnly)) return new DateOnly(2023, 1, 2);
        if (type == typeof(TimeOnly)) return new TimeOnly(9, 8, 7);
        if (type == typeof(DateTime)) return mapping.DatabaseTypeName.Equals("DATE", StringComparison.OrdinalIgnoreCase)
            ? new DateTime(2023, 1, 2, 0, 0, 0, DateTimeKind.Unspecified)
            : mapping.DatabaseTypeName.Equals("timestamp with time zone", StringComparison.OrdinalIgnoreCase)
                ? new DateTime(2023, 1, 2, 9, 8, 7, DateTimeKind.Utc)
                : new DateTime(2023, 1, 2, 9, 8, 7, DateTimeKind.Unspecified);
        if (type == typeof(DateTimeOffset)) return new DateTimeOffset(2023, 1, 2, 9, 8, 7, TimeSpan.Zero);
        if (type == typeof(IPAddress)) return IPAddress.Parse("2001:db8::1");
        if (type == typeof(IPNetwork)) return IPNetwork.Parse("198.51.100.0/24");
        if (type == typeof(PhysicalAddress)) return PhysicalAddress.Parse(mapping.DatabaseTypeName.Equals("macaddr8", StringComparison.OrdinalIgnoreCase) ? "8899AABBCCDDEEFF" : "8899AABBCCDD");
        if (type == typeof(TimeSpan)) return new TimeSpan(3, 4, 5);
        if (type == typeof(char)) return 'R';

        throw new InvalidOperationException($"No centrally defined alternate value is available for CLR type '{type.FullName}'.");
    }

    private static bool IsNpgsqlRange(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition().FullName == "NpgsqlTypes.NpgsqlRange`1";

    private static object CreateRange(Type rangeType, NativeTypeMapping mapping, bool alternate)
    {
        var elementType = rangeType.GetGenericArguments()[0];
        object lower;
        object upper;
        if (elementType == typeof(int))
        {
            lower = alternate ? 10 : 1;
            upper = alternate ? 20 : 5;
        }
        else if (elementType == typeof(long))
        {
            lower = alternate ? 100L : 1L;
            upper = alternate ? 200L : 5L;
        }
        else if (elementType == typeof(decimal))
        {
            lower = alternate ? 10.25m : 1.25m;
            upper = alternate ? 20.75m : 5.75m;
        }
        else if (elementType == typeof(DateOnly))
        {
            lower = alternate ? new DateOnly(2023, 1, 1) : new DateOnly(2024, 1, 1);
            upper = alternate ? new DateOnly(2023, 12, 31) : new DateOnly(2024, 12, 31);
        }
        else if (elementType == typeof(DateTime))
        {
            var isTimeZoneRange = mapping.DatabaseTypeName.Equals("tstzrange", StringComparison.OrdinalIgnoreCase);
            var kind = isTimeZoneRange ? DateTimeKind.Utc : DateTimeKind.Unspecified;
            lower = alternate ? new DateTime(2023, 1, 1, 0, 0, 0, kind) : new DateTime(2024, 1, 1, 0, 0, 0, kind);
            upper = alternate ? new DateTime(2023, 12, 31, 0, 0, 0, kind) : new DateTime(2024, 12, 31, 0, 0, 0, kind);
        }
        else
        {
            throw new InvalidOperationException($"No centrally defined range bounds are available for '{elementType.FullName}'.");
        }

        return Activator.CreateInstance(rangeType, lower, true, upper, false)
               ?? throw new InvalidOperationException($"Could not construct a sample for range type '{rangeType.FullName}'.");
    }

    private static Array CreateArray(Type elementType, NativeTypeMapping mapping)
    {
        var values = elementType == typeof(int)
            ? new object[] { 1, 0, 5 }
            : elementType == typeof(string)
                ? mapping.DatabaseTypeName.Contains("json", StringComparison.OrdinalIgnoreCase)
                    ? new object[] { "{\"value\":1}", "[]" }
                    : new object[] { "A", "B" }
                : throw new InvalidOperationException($"No centrally defined representative array is available for CLR type '{elementType.FullName}[]'.");
        var array = Array.CreateInstance(elementType, values.Length);
        for (var index = 0; index < values.Length; index++) array.SetValue(values[index], index);
        return array;
    }
}
