using System.Collections;
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
                data.Add(probe.Index, probe.Mapping.DatabaseTypeName, probe.Mapping.ClrType.FullName ?? probe.Mapping.ClrType.Name);
            }

            if (data.Count == 0)
            {
                data.Add(-1, "NO-INDEPENDENT-TYPE-PROFILE", "none");
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

    private static NativeTypeProbeDefinition GetProbe(int index, string databaseTypeName, string clrTypeName)
    {
        var probe = ProviderTestRuntime.NativeTypeProbes.SingleOrDefault(item => item.Index == index);
        Assert.NotNull(probe);
        Assert.Equal(databaseTypeName, probe.Mapping.DatabaseTypeName);
        Assert.Equal(clrTypeName, probe.Mapping.ClrType.FullName ?? probe.Mapping.ClrType.Name);
        return probe;
    }

    private static Dictionary<string, object> CreateRow(Guid id, object value) => new()
    {
        ["id"] = id,
        ["value"] = value
    };

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
        if (type == typeof(byte[])) return new byte[] { 0x4a };
        if (type.IsArray)
        {
            return CreateArray(type.GetElementType()!, mapping);
        }

        if (type == typeof(string) && mapping.DatabaseTypeName.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return "{\"value\":1}";
        }

        if (type == typeof(bool)) return true;
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
        if (type == typeof(decimal)) return 123456789.1234m;
        if (type == typeof(float)) return 1234.5f;
        if (type == typeof(double)) return 9876.125d;
        if (type == typeof(string)) return "A";
        if (type == typeof(Guid)) return Guid.Parse("5d37ddf5-fab9-4e5b-95fb-b5259a5e3d31");
        if (type == typeof(DateOnly)) return new DateOnly(2024, 2, 29);
        if (type == typeof(TimeOnly)) return new TimeOnly(13, 14, 15);
        if (type == typeof(DateTime)) return new DateTime(2024, 2, 29, 13, 14, 15, DateTimeKind.Unspecified);
        if (type == typeof(DateTimeOffset)) return new DateTimeOffset(2024, 2, 29, 13, 14, 15, TimeSpan.Zero);
        if (type == typeof(TimeSpan)) return TimeSpan.FromHours(1) + TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(3);
        if (type == typeof(char)) return 'Q';

        throw new InvalidOperationException($"No centrally defined representative value is available for CLR type '{type.FullName}'.");
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
