using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;

var outputPath = args.Length == 0 ? "efcore-10-api-inventory.json" : args[0];
var coverageByApiSelector = args.Length > 1
    ? LoadCoverage(Path.GetFullPath(args[1]))
    : new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
var assemblies = new[]
{
    typeof(DbContext).Assembly,
    Assembly.Load("Microsoft.EntityFrameworkCore.Abstractions"),
    typeof(RelationalDatabaseFacadeExtensions).Assembly
}.Distinct().ToArray();

var entries = assemblies
    .SelectMany(assembly => assembly.GetExportedTypes())
    .Where(type => type.IsPublic || type.IsNestedPublic)
    .OrderBy(type => type.FullName, StringComparer.Ordinal)
    .SelectMany(type => new[]
        {
            new ApiInventoryEntry(
                type.Assembly.GetName().Name ?? "unknown",
                type.FullName ?? type.Name,
                "Type",
                null,
                Format(type),
                IsExcluded(type) ? false : coverageByApiSelector.ContainsKey(type.FullName ?? type.Name) ? true : null,
                coverageByApiSelector.TryGetValue(type.FullName ?? type.Name, out var typeContracts) ? typeContracts.Order(StringComparer.Ordinal).ToArray() : Array.Empty<string>(),
                EntryDisposition(type, coverageByApiSelector.ContainsKey(type.FullName ?? type.Name)))
        }
        .Concat(GetPublicMembers(type).Select(member => new ApiInventoryEntry(
            type.Assembly.GetName().Name ?? "unknown",
            type.FullName ?? type.Name,
            member.MemberType.ToString(),
            member.Name,
            Format(member),
            IsExcluded(member) ? false : coverageByApiSelector.ContainsKey($"{type.FullName}.{member.Name}") ? true : null,
            coverageByApiSelector.TryGetValue($"{type.FullName}.{member.Name}", out var contracts) ? contracts.Order(StringComparer.Ordinal).ToArray() : Array.Empty<string>(),
            EntryDisposition(member, coverageByApiSelector.ContainsKey($"{type.FullName}.{member.Name}"))))))
    .ToArray();

var inventory = new ApiInventoryDocument(
    Contract: "EF Core 10 public API inventory",
    DotNetRuntime: Environment.Version.ToString(),
    Assemblies: assemblies.Select(assembly => new ApiAssembly(
        assembly.GetName().Name ?? "unknown",
        assembly.GetName().Version?.ToString() ?? "unknown")).ToArray(),
    Summary: new ApiInventorySummary(
        entries.Length,
        entries.Count(entry => entry.MemberKind == "Type"),
        entries.Count(entry => entry.MemberKind != "Type"),
        entries.Count(entry => entry.Disposition == "unclassified"),
        entries.Count(entry => entry.Disposition.StartsWith("excluded-", StringComparison.Ordinal)),
        entries.Count(entry => entry.Disposition == "behavior-tested-family"),
        entries.Where(entry => entry.MemberKind != "Type" && !entry.Disposition.StartsWith("excluded-", StringComparison.Ordinal))
            .Select(entry => entry.ApiSelector)
            .Distinct(StringComparer.Ordinal)
            .Count(selector => selector is null || !coverageByApiSelector.ContainsKey(selector))),
    CoverageSelectors: new CoverageSelectorReport(
        coverageByApiSelector.Keys.Where(selector => selector.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal) &&
                                                       !entries.Any(entry => entry.ApiSelector == selector))
            .Order(StringComparer.Ordinal).ToArray(),
        coverageByApiSelector.Keys.Where(selector => !selector.StartsWith("Microsoft.EntityFrameworkCore.", StringComparison.Ordinal) &&
                                                       !entries.Any(entry => entry.ApiSelector == selector))
            .Order(StringComparer.Ordinal).ToArray()),
    Entries: entries);

await using var output = File.Create(outputPath);
await JsonSerializer.SerializeAsync(output, inventory, new JsonSerializerOptions { WriteIndented = true });
Console.WriteLine($"Wrote {entries.Length} public API entries; {inventory.Summary.MemberEntriesInCoveredFamilies} overload entries belong to tested API families; {inventory.Summary.UncoveredMemberFamilies} member families remain uncovered.");

static IEnumerable<MemberInfo> GetPublicMembers(Type type) => type
    .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
    .Where(member => member switch
    {
        MethodInfo method => !(method.IsSpecialName &&
                              (method.Name.StartsWith("get_", StringComparison.Ordinal) ||
                               method.Name.StartsWith("set_", StringComparison.Ordinal) ||
                               method.Name.StartsWith("add_", StringComparison.Ordinal) ||
                               method.Name.StartsWith("remove_", StringComparison.Ordinal))),
        _ => true
    });

static string Disposition(MemberInfo member)
{
    var declaringType = member as Type ?? member.DeclaringType;
    if (declaringType?.Namespace?.Split('.').Contains("Internal", StringComparer.Ordinal) == true)
    {
        return "excluded-ef-internal";
    }

    var attributes = member.GetCustomAttributesData()
        .Concat(member is not Type ? member.DeclaringType?.GetCustomAttributesData() ?? [] : [])
        .ToArray();
    if (attributes.Any(attribute => attribute.AttributeType.Name == "EntityFrameworkInternalAttribute"))
    {
        return "excluded-ef-internal";
    }

    var obsoleteError = attributes.Any(attribute =>
        attribute.AttributeType == typeof(ObsoleteAttribute) &&
        (attribute.ConstructorArguments.Count > 1 && attribute.ConstructorArguments[1].Value is true ||
         attribute.NamedArguments.Any(argument => argument.MemberName == "IsError" && argument.TypedValue.Value is true)));
    return obsoleteError ? "excluded-obsolete-error" : "unclassified";
}

static bool IsExcluded(MemberInfo member) => Disposition(member).StartsWith("excluded-", StringComparison.Ordinal);

static string EntryDisposition(MemberInfo member, bool hasBehavioralCoverage)
{
    var disposition = Disposition(member);
    return disposition.StartsWith("excluded-", StringComparison.Ordinal)
        ? disposition
        : hasBehavioralCoverage ? "behavior-tested-family" : disposition;
}

static Dictionary<string, HashSet<string>> LoadCoverage(string testAssemblyPath)
{
    var assembly = Assembly.LoadFrom(testAssemblyPath);
    var coverage = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    foreach (var type in assembly.GetTypes())
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            foreach (var attribute in CustomAttributeData.GetCustomAttributes(method)
                         .Where(attribute => attribute.AttributeType == typeof(BehavioralCoverageAttribute)))
            {
                var contractId = (string)attribute.ConstructorArguments[0].Value!;
                if (attribute.ConstructorArguments.Count < 2 || attribute.ConstructorArguments[1].Value is not IReadOnlyCollection<CustomAttributeTypedArgument> selectors)
                {
                    continue;
                }

                foreach (var selector in selectors.Select(item => item.Value as string).Where(value => value is not null))
                {
                    if (!coverage.TryGetValue(selector!, out var contracts))
                    {
                        contracts = new HashSet<string>(StringComparer.Ordinal);
                        coverage.Add(selector!, contracts);
                    }

                    contracts.Add(contractId);
                }
            }
        }
    }

    return coverage;
}

static string Format(MemberInfo member) => member switch
{
    Type type => $"{(type.IsInterface ? "interface" : type.IsEnum ? "enum" : type.IsValueType ? "struct" : "class")} {TypeName(type)}",
    MethodInfo method => $"{TypeName(method.ReturnType)} {method.Name}{GenericParameters(method)}({string.Join(", ", method.GetParameters().Select(FormatParameter))}){GenericConstraints(method)}",
    ConstructorInfo constructor => $".ctor({string.Join(", ", constructor.GetParameters().Select(FormatParameter))})",
    PropertyInfo property => $"{TypeName(property.PropertyType)} {property.Name}{(property.GetIndexParameters().Length == 0 ? string.Empty : $"[{string.Join(", ", property.GetIndexParameters().Select(FormatParameter))}]")}{(property.CanRead ? " get;" : string.Empty)}{(property.CanWrite ? " set;" : string.Empty)}",
    EventInfo eventInfo => $"event {TypeName(eventInfo.EventHandlerType ?? typeof(void))} {eventInfo.Name}",
    FieldInfo field => $"{TypeName(field.FieldType)} {field.Name}{(field.IsLiteral ? $" = {Constant(field.GetRawConstantValue())}" : string.Empty)}",
    _ => member.ToString() ?? member.Name
};

static string TypeName(Type type)
{
    if (type.IsGenericParameter)
    {
        return type.Name;
    }

    if (type.IsByRef)
    {
        return $"{TypeName(type.GetElementType()!)}&";
    }

    if (type.IsPointer)
    {
        return $"{TypeName(type.GetElementType()!)}*";
    }

    if (type.IsArray)
    {
        return $"{TypeName(type.GetElementType()!)}[{new string(',', type.GetArrayRank() - 1)}]";
    }

    if (type.IsGenericType)
    {
        var name = type.GetGenericTypeDefinition().FullName ?? type.Name;
        var arity = name.IndexOf('`');
        if (arity >= 0)
        {
            name = name[..arity];
        }

        return $"{name}<{string.Join(", ", type.GetGenericArguments().Select(TypeName))}>";
    }

    return type.FullName ?? type.Name;
}

static string GenericParameters(MethodInfo method) => method.IsGenericMethodDefinition
    ? $"<{string.Join(", ", method.GetGenericArguments().Select(argument => argument.Name))}>"
    : string.Empty;

static string GenericConstraints(MethodInfo method) => method.IsGenericMethodDefinition
    ? string.Concat(method.GetGenericArguments().Select(argument =>
    {
        var constraints = argument.GetGenericParameterConstraints().Select(TypeName).ToList();
        var attributes = argument.GenericParameterAttributes & GenericParameterAttributes.SpecialConstraintMask;
        if ((attributes & GenericParameterAttributes.ReferenceTypeConstraint) != 0) constraints.Insert(0, "class");
        if ((attributes & GenericParameterAttributes.NotNullableValueTypeConstraint) != 0) constraints.Insert(0, "struct");
        if ((attributes & GenericParameterAttributes.DefaultConstructorConstraint) != 0) constraints.Add("new()");
        return constraints.Count == 0 ? string.Empty : $" where {argument.Name} : {string.Join(", ", constraints)}";
    }))
    : string.Empty;

static string FormatParameter(ParameterInfo parameter)
{
    var type = parameter.ParameterType;
    var modifier = string.Empty;
    if (type.IsByRef)
    {
        modifier = parameter.IsOut ? "out " : parameter.IsIn ? "in " : "ref ";
        type = type.GetElementType()!;
    }

    var defaultValue = parameter.IsOptional ? $" = {Constant(parameter.DefaultValue)}" : string.Empty;
    return $"{modifier}{TypeName(type)} {parameter.Name}{defaultValue}";
}

static string Constant(object? value) => value switch
{
    null => "null",
    DBNull => "default",
    Missing => "default",
    string text => $"\"{text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"",
    char character => $"'{character}'",
    bool boolean => boolean ? "true" : "false",
    _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "default"
};

internal sealed record ApiAssembly(string Name, string Version);
internal sealed record ApiInventorySummary(int TotalEntries, int PublicTypes, int PublicMembers, int AwaitingClassification, int ExplicitlyExcluded, int MemberEntriesInCoveredFamilies, int UncoveredMemberFamilies);
internal sealed record CoverageSelectorReport(string[] UnmatchedEfCoreSelectors, string[] ExternalApiSelectors);
internal sealed record ApiInventoryEntry(string AssemblyName, string DeclaringType, string MemberKind, string? ApiMemberName, string Signature, bool? ProviderApplicable, string[] BehavioralContracts, string Disposition)
{
    public string? ApiSelector => MemberKind == "Type" ? DeclaringType : ApiMemberName is null ? null : $"{DeclaringType}.{ApiMemberName}";
}
internal sealed record ApiInventoryDocument(string Contract, string DotNetRuntime, ApiAssembly[] Assemblies, ApiInventorySummary Summary, CoverageSelectorReport CoverageSelectors, ApiInventoryEntry[] Entries);
