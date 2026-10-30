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
                                          storeType.Equals("xml", StringComparison.OrdinalIgnoreCase) ||
                                          storeType.Equals("point", StringComparison.OrdinalIgnoreCase) ||
                                          storeType.Equals("polygon", StringComparison.OrdinalIgnoreCase) ||
                                          type == typeof(BitArray) ||
                                          storeType.Equals("cid", StringComparison.OrdinalIgnoreCase) ||
                                          storeType.Equals("xid8", StringComparison.OrdinalIgnoreCase);
        return !type.IsArray && type != typeof(byte[]) && !hasProviderSpecificEquality;
    }

    private static void AssertValueEqual(object expected, object? actual, NativeTypeMapping mapping)
    {
        var equal = mapping.DatabaseTypeName.Equals("time with time zone", StringComparison.OrdinalIgnoreCase) ||
                    mapping.DatabaseTypeName.Equals("time with time zone[]", StringComparison.OrdinalIgnoreCase)
            ? TimeWithTimeZoneValuesEqual(expected, actual)
            : expected is BitArray expectedBits && actual is BitArray actualBits
                ? expectedBits.Cast<bool>().SequenceEqual(actualBits.Cast<bool>())
                : expected is BitArray[] expectedBitArrays && actual is BitArray[] actualBitArrays
                    ? expectedBitArrays.Length == actualBitArrays.Length && expectedBitArrays.Zip(actualBitArrays)
                        .All(pair => pair.First.Cast<bool>().SequenceEqual(pair.Second.Cast<bool>()))
            : expected is string[] expectedJsonValues && actual is string[] actualJsonValues &&
              IsJsonStoreType(mapping.DatabaseTypeName)
                ? expectedJsonValues.Length == actualJsonValues.Length &&
                  expectedJsonValues.Zip(actualJsonValues).All(pair => JsonNode.DeepEquals(JsonNode.Parse(pair.First), JsonNode.Parse(pair.Second)))
            : expected is string expectedText && actual is string actualText &&
              IsJsonStoreType(mapping.DatabaseTypeName)
                ? JsonNode.DeepEquals(JsonNode.Parse(expectedText), JsonNode.Parse(actualText))
                : StructuralComparisons.StructuralEqualityComparer.Equals(expected, actual);
        Assert.True(
            equal,
            $"Native type '{mapping.DatabaseTypeName}' mapped from CLR type '{mapping.ClrType.FullName}' did not round-trip the declared sample value. Expected: {FormatValue(expected)}; actual: {FormatValue(actual)}.");
    }

    private static string FormatValue(object? value) => value is IEnumerable sequence and not string
        ? $"[{string.Join(", ", sequence.Cast<object?>().Select(item => item?.ToString() ?? "null"))}]"
        : value?.ToString() ?? "null";

    private static bool IsJsonStoreType(string storeType) =>
        storeType.Equals("json", StringComparison.OrdinalIgnoreCase) ||
        storeType.Equals("jsonb", StringComparison.OrdinalIgnoreCase) ||
        storeType.Equals("json[]", StringComparison.OrdinalIgnoreCase) ||
        storeType.Equals("jsonb[]", StringComparison.OrdinalIgnoreCase);

    private static bool TimeWithTimeZoneValuesEqual(object expected, object? actual)
    {
        static bool SameTimeAndOffset(DateTimeOffset left, DateTimeOffset right) =>
            left.TimeOfDay == right.TimeOfDay && left.Offset == right.Offset;

        if (expected is DateTimeOffset expectedValue && actual is DateTimeOffset actualValue)
        {
            return SameTimeAndOffset(expectedValue, actualValue);
        }

        if (expected is DateTimeOffset[] expectedValues && actual is DateTimeOffset[] actualValues)
        {
            return expectedValues.Length == actualValues.Length &&
                   expectedValues.Zip(actualValues).All(pair => SameTimeAndOffset(pair.First, pair.Second));
        }

        return false;
    }
}

internal static class NativeTypeSampleFactory
{
    public static object Create(NativeTypeMapping mapping)
    {
        var type = Nullable.GetUnderlyingType(mapping.ClrType) ?? mapping.ClrType;
        if (TryCreateNpgsqlValue(type, alternate: false, out var providerValue)) return providerValue;
        if (IsNpgsqlRange(type)) return CreateRange(type, mapping, alternate: false);
        if (type == typeof(BitArray)) return new BitArray(mapping.DatabaseTypeName.Equals("bit", StringComparison.OrdinalIgnoreCase) ? [true] : [true, false, true]);
        if (type == typeof(byte[])) return new byte[] { 0x4a };
        if (type.IsArray)
        {
            return CreateArray(type.GetElementType()!, mapping, alternate: false);
        }

        if (type == typeof(string) && mapping.DatabaseTypeName.Equals("jsonpath", StringComparison.OrdinalIgnoreCase)) return "$.\"value\"";
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
        if (type == typeof(uint)) return mapping.DatabaseTypeName is "oid" or "xid" or "cid" ? 12345u : uint.MaxValue;
        if (type == typeof(int)) return mapping.DatabaseTypeName.Equals("YEAR", StringComparison.OrdinalIgnoreCase)
            ? 2024
            : mapping.DatabaseTypeName.Equals("MEDIUMINT", StringComparison.OrdinalIgnoreCase) ? -12345 : int.MinValue;
        if (type == typeof(uint)) return mapping.DatabaseTypeName.Equals("MEDIUMINT", StringComparison.OrdinalIgnoreCase)
            ? 12345u
            : uint.MaxValue;
        if (type == typeof(long)) return long.MinValue;
        if (type == typeof(ulong)) return mapping.DatabaseTypeName.Equals("xid8", StringComparison.OrdinalIgnoreCase) ? 1234567890123ul : ulong.MaxValue;
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
        if (type == typeof(DateTimeOffset)) return mapping.DatabaseTypeName.Equals("time with time zone", StringComparison.OrdinalIgnoreCase)
            ? new DateTimeOffset(1, 1, 1, 13, 14, 15, TimeSpan.Zero)
            : new DateTimeOffset(2024, 2, 29, 13, 14, 15, TimeSpan.Zero);
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
        if (TryCreateNpgsqlValue(type, alternate: true, out var providerValue)) return providerValue;
        if (IsNpgsqlRange(type)) return CreateRange(type, mapping, alternate: true);
        if (type == typeof(BitArray)) return new BitArray(mapping.DatabaseTypeName.Equals("bit", StringComparison.OrdinalIgnoreCase) ? [false] : [false, true, false]);
        if (type == typeof(byte[])) return new byte[] { 0x52 };
        if (type.IsArray) return CreateArray(type.GetElementType()!, mapping, alternate: true);
        if (type == typeof(string) && mapping.DatabaseTypeName.Equals("jsonpath", StringComparison.OrdinalIgnoreCase)) return "$.\"other\"";
        if (type == typeof(string) && mapping.DatabaseTypeName.Contains("json", StringComparison.OrdinalIgnoreCase)) return "{\"value\":2}";
        if (type == typeof(string) && mapping.DatabaseTypeName.Equals("xml", StringComparison.OrdinalIgnoreCase)) return "<v>2</v>";
        if (type == typeof(bool)) return false;
        if (type == typeof(ulong) && mapping.DatabaseTypeName.Equals("BIT", StringComparison.OrdinalIgnoreCase)) return 0ul;
        if (type == typeof(sbyte)) return sbyte.MaxValue;
        if (type == typeof(byte)) return (byte)0;
        if (type == typeof(short)) return short.MaxValue;
        if (type == typeof(ushort)) return (ushort)0;
        if (type == typeof(uint)) return mapping.DatabaseTypeName is "oid" or "xid" or "cid" ? 12346u : 0u;
        if (type == typeof(int)) return mapping.DatabaseTypeName.Equals("YEAR", StringComparison.OrdinalIgnoreCase)
            ? 2023
            : mapping.DatabaseTypeName.Equals("MEDIUMINT", StringComparison.OrdinalIgnoreCase) ? 12345 : int.MaxValue;
        if (type == typeof(uint)) return 0u;
        if (type == typeof(long)) return long.MaxValue;
        if (type == typeof(ulong)) return mapping.DatabaseTypeName.Equals("xid8", StringComparison.OrdinalIgnoreCase) ? 987654321ul : 0ul;
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
        if (type == typeof(DateTimeOffset)) return mapping.DatabaseTypeName.Equals("time with time zone", StringComparison.OrdinalIgnoreCase)
            ? new DateTimeOffset(1, 1, 1, 9, 8, 7, TimeSpan.Zero)
            : new DateTimeOffset(2023, 1, 2, 9, 8, 7, TimeSpan.Zero);
        if (type == typeof(IPAddress)) return IPAddress.Parse("2001:db8::1");
        if (type == typeof(IPNetwork)) return IPNetwork.Parse("198.51.100.0/24");
        if (type == typeof(PhysicalAddress)) return PhysicalAddress.Parse(mapping.DatabaseTypeName.Equals("macaddr8", StringComparison.OrdinalIgnoreCase) ? "8899AABBCCDDEEFF" : "8899AABBCCDD");
        if (type == typeof(TimeSpan)) return new TimeSpan(3, 4, 5);
        if (type == typeof(char)) return 'R';

        throw new InvalidOperationException($"No centrally defined alternate value is available for CLR type '{type.FullName}'.");
    }

    private static bool IsNpgsqlRange(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition().FullName == "NpgsqlTypes.NpgsqlRange`1";

    private static bool TryCreateNpgsqlValue(Type type, bool alternate, out object value)
    {
        var fullName = type.FullName;
        var marker = alternate ? 2d : 1d;
        if (fullName == "NpgsqlTypes.NpgsqlPoint")
        {
            value = Activator.CreateInstance(type, marker, marker + 1)!;
            return true;
        }
        if (fullName == "NpgsqlTypes.NpgsqlTid")
        {
            value = Activator.CreateInstance(type, alternate ? 23456u : 12345u, alternate ? (ushort)2 : (ushort)1)!;
            return true;
        }
        if (fullName == "NpgsqlTypes.NpgsqlLSeg")
        {
            var pointType = type.Assembly.GetType("NpgsqlTypes.NpgsqlPoint")!;
            var first = Activator.CreateInstance(pointType, marker, marker + 1)!;
            var second = Activator.CreateInstance(pointType, marker + 2, marker + 3)!;
            value = Activator.CreateInstance(type, first, second)!;
            return true;
        }
        if (fullName == "NpgsqlTypes.NpgsqlPath" || fullName == "NpgsqlTypes.NpgsqlPolygon")
        {
            var pointType = type.Assembly.GetType("NpgsqlTypes.NpgsqlPoint")!;
            var points = Array.CreateInstance(pointType, 3);
            for (var index = 0; index < points.Length; index++)
                points.SetValue(Activator.CreateInstance(pointType, marker + index, marker + index + 1), index);
            value = fullName.EndsWith("NpgsqlPath", StringComparison.Ordinal)
                ? Activator.CreateInstance(type, points, true)!
                : Activator.CreateInstance(type, points)!;
            return true;
        }
        if (fullName == "NpgsqlTypes.NpgsqlBox" || fullName == "NpgsqlTypes.NpgsqlCircle")
        {
            var pointType = type.Assembly.GetType("NpgsqlTypes.NpgsqlPoint")!;
            var first = Activator.CreateInstance(pointType, marker + 4, marker + 5)!;
            if (fullName.EndsWith("NpgsqlBox", StringComparison.Ordinal))
            {
                var second = Activator.CreateInstance(pointType, marker, marker + 1)!;
                value = Activator.CreateInstance(type, first, second)!;
            }
            else
            {
                value = Activator.CreateInstance(type, first, marker + 2)!;
            }
            return true;
        }
        if (fullName == "NpgsqlTypes.NpgsqlLine")
        {
            value = Activator.CreateInstance(type, marker, marker + 1, marker + 2)!;
            return true;
        }
        if (fullName == "NpgsqlTypes.NpgsqlLogSequenceNumber")
        {
            value = Activator.CreateInstance(type, alternate ? 987654321ul : 123456789ul)!;
            return true;
        }
        if (fullName is "NpgsqlTypes.NpgsqlTsVector" or "NpgsqlTypes.NpgsqlTsQuery")
        {
            var parse = type.GetMethod("Parse", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, [typeof(string)])!;
            value = parse.Invoke(null, [fullName.EndsWith("TsVector", StringComparison.Ordinal)
                ? alternate ? "other:1 value:2" : "value:1 sample:2"
                : alternate ? "other & sample" : "value & sample"])!;
            return true;
        }
        value = null!;
        return false;
    }

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
            var isTimeZoneRange = mapping.DatabaseTypeName.StartsWith("tstz", StringComparison.OrdinalIgnoreCase);
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

    private static Array CreateArray(Type elementType, NativeTypeMapping mapping, bool alternate)
    {
        object[] values;
        if (IsNpgsqlRange(elementType)) values = [CreateRange(elementType, mapping, alternate)];
        else if (TryCreateNpgsqlValue(elementType, alternate: false, out var providerSample) &&
                 TryCreateNpgsqlValue(elementType, alternate: true, out var providerAlternate))
            values = alternate ? [providerAlternate, providerSample] : [providerSample, providerAlternate];
        else if (elementType == typeof(bool)) values = alternate ? [false, true] : [true, false];
        else if (elementType == typeof(short)) values = alternate ? [(short)5, (short)0] : [(short)1, (short)-1];
        else if (elementType == typeof(ushort)) values = alternate ? [(ushort)5, (ushort)0] : [(ushort)1, ushort.MaxValue];
        else if (elementType == typeof(uint)) values = alternate ? [23456u, 0u] : [12345u, 67890u];
        else if (elementType == typeof(ulong)) values = alternate ? [987654321ul, 0ul] : [1234567890123ul, 987654321ul];
        else if (elementType == typeof(int)) values = alternate ? [5, 0, 1] : [1, 0, 5];
        else if (elementType == typeof(long)) values = alternate ? [5L, 0L] : [1L, -1L];
        else if (elementType == typeof(float)) values = alternate ? [-5.5f, 0f] : [1.25f, -1.5f];
        else if (elementType == typeof(double)) values = alternate ? [-5.5d, 0d] : [1.25d, -1.5d];
        else if (elementType == typeof(decimal)) values = alternate ? [-5.5m, 0m] : [1.25m, -1.5m];
        else if (elementType == typeof(string)) values = mapping.DatabaseTypeName.Contains("jsonpath", StringComparison.OrdinalIgnoreCase)
            ? alternate ? ["$.\"other\"", "$.\"value\""] : ["$.\"value\"", "$.\"other\""]
            : mapping.DatabaseTypeName.Contains("json", StringComparison.OrdinalIgnoreCase)
                ? alternate ? ["{\"value\":2}", "[]"] : ["{\"value\":1}", "[]"]
                : mapping.DatabaseTypeName.Contains("xml", StringComparison.OrdinalIgnoreCase)
                ? alternate ? ["<v>2</v>", "<v>3</v>"] : ["<v>1</v>", "<v>3</v>"]
                : alternate ? ["C", "D"] : ["A", "B"];
        else if (elementType == typeof(char)) values = alternate ? ['R', 'S'] : ['Q', 'T'];
        else if (elementType == typeof(BitArray)) values = mapping.DatabaseTypeName.Equals("bit[]", StringComparison.OrdinalIgnoreCase)
            ? alternate
                ? [new BitArray([false]), new BitArray([true])]
                : [new BitArray([true]), new BitArray([false])]
            : alternate
                ? [new BitArray([false, true, false]), new BitArray([true, false])]
                : [new BitArray([true, false, true]), new BitArray([false, true])];
        else if (elementType == typeof(byte[])) values = alternate
            ? [new byte[] { 0x52 }, new byte[] { 0x53 }]
            : [new byte[] { 0x4a }, new byte[] { 0x4b }];
        else if (elementType == typeof(Guid)) values = alternate
            ? [Guid.Parse("84c5903f-330f-42d1-92f4-94ab241b1b32"), Guid.Empty]
            : [Guid.Parse("5d37ddf5-fab9-4e5b-95fb-b5259a5e3d31"), Guid.Empty];
        else if (elementType == typeof(DateOnly)) values = alternate
            ? [new DateOnly(2023, 1, 2), new DateOnly(2023, 1, 3)]
            : [new DateOnly(2024, 2, 29), new DateOnly(2024, 3, 1)];
        else if (elementType == typeof(TimeOnly)) values = alternate
            ? [new TimeOnly(9, 8, 7), new TimeOnly(10, 8, 7)]
            : [new TimeOnly(13, 14, 15), new TimeOnly(14, 14, 15)];
        else if (elementType == typeof(DateTime))
        {
            var kind = mapping.DatabaseTypeName.Contains("with time zone", StringComparison.OrdinalIgnoreCase)
                ? DateTimeKind.Utc
                : DateTimeKind.Unspecified;
            values = alternate
                ? [new DateTime(2023, 1, 2, 9, 8, 7, kind), new DateTime(2023, 1, 3, 9, 8, 7, kind)]
                : [new DateTime(2024, 2, 29, 13, 14, 15, kind), new DateTime(2024, 3, 1, 13, 14, 15, kind)];
        }
        else if (elementType == typeof(DateTimeOffset)) values = alternate
            ? [new DateTimeOffset(1, 1, 1, 9, 8, 7, TimeSpan.Zero), new DateTimeOffset(1, 1, 1, 10, 8, 7, TimeSpan.Zero)]
            : [new DateTimeOffset(1, 1, 1, 13, 14, 15, TimeSpan.Zero), new DateTimeOffset(1, 1, 1, 14, 14, 15, TimeSpan.Zero)];
        else if (elementType == typeof(TimeSpan)) values = alternate
            ? [TimeSpan.FromHours(3), TimeSpan.FromHours(4)]
            : [TimeSpan.FromHours(1), TimeSpan.FromHours(2)];
        else if (elementType == typeof(IPAddress)) values = alternate
            ? [IPAddress.Parse("2001:db8::1"), IPAddress.Parse("2001:db8::2")]
            : [IPAddress.Parse("192.0.2.1"), IPAddress.Parse("192.0.2.2")];
        else if (elementType == typeof(IPNetwork)) values = alternate
            ? [IPNetwork.Parse("198.51.100.0/24"), IPNetwork.Parse("198.51.101.0/24")]
            : [IPNetwork.Parse("192.0.2.0/24"), IPNetwork.Parse("192.0.3.0/24")];
        else if (elementType == typeof(PhysicalAddress))
        {
            var isMacAddress8 = mapping.DatabaseTypeName.Contains("macaddr8", StringComparison.OrdinalIgnoreCase);
            values = alternate
                ? isMacAddress8
                    ? [PhysicalAddress.Parse("8899AABBCCDDEEFF"), PhysicalAddress.Parse("8899AABBCCDDEEFE")]
                    : [PhysicalAddress.Parse("8899AABBCCDD"), PhysicalAddress.Parse("8899AABBCCDE")]
                : isMacAddress8
                    ? [PhysicalAddress.Parse("0011223344556677"), PhysicalAddress.Parse("0011223344556678")]
                    : [PhysicalAddress.Parse("001122334455"), PhysicalAddress.Parse("001122334456")];
        }
        else throw new InvalidOperationException($"No centrally defined representative array is available for CLR type '{elementType.FullName}[]'.");

        var array = Array.CreateInstance(elementType, values.Length);
        for (var index = 0; index < values.Length; index++) array.SetValue(values[index], index);
        return array;
    }
}
