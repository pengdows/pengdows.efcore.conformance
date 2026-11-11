using System.Diagnostics;
using System.Data.Common;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using pengdows.efcore.conformance;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    PrintUsage();
    return args.Length > 0 ? 0 : 2;
}

if (args[0] == "--compare")
{
    return await CompareReportsAsync(args);
}

if (args[0] == "--enumerate-cases")
{
    return await EnumerateCasesAsync(args);
}

if (args[0] == "--completeness")
{
    return await CompletenessAsync(args);
}

if (args[0] == "--contract-gate")
{
    return await ContractGateAsync(args);
}

if (args[0] == "--verify-defect-control")
{
    return await VerifyDefectControlAsync(args);
}

if (args[0] == "--run")
{
    return await RunTestsAsync(args);
}

if (args[0] == "--run-profile")
{
    return await RunProfileTestsAsync(args);
}

var discoverTypes = args[0] == "--discover-types";
var offset = discoverTypes ? 1 : 0;
if (discoverTypes ? args.Length is < 3 or > 5 : args.Length != 2)
{
    Console.Error.WriteLine("Invalid arguments. Use --help for usage.");
    return 2;
}

var connectionString = GetConnectionString();
var adapter = LoadAdapter(args[offset], args[offset + 1]);
adapter.Capabilities.Validate();
if (discoverTypes)
{
    if (adapter is not IAdoNetProviderAdapter adoNetAdapter)
    {
        Console.Error.WriteLine($"Adapter must implement {nameof(IAdoNetProviderAdapter)} for type discovery.");
        return 2;
    }

    await using var connection = adoNetAdapter.CreateConnection(connectionString);
    var typeReport = DiscoverTypeReport(connection, adapter);
    if (args.Length == 5)
    {
        typeReport = typeReport with
        {
            NativeTypeCoverage = BuildNativeTypeCoverage(typeReport, Path.GetFullPath(args[4]), adapter.Name)
        };
    }

    var typeJson = JsonSerializer.Serialize(typeReport, new JsonSerializerOptions { WriteIndented = true });
    if (args.Length >= 4)
    {
        var outputPath = Path.GetFullPath(args[3]);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        await File.WriteAllTextAsync(outputPath, typeJson);
        Console.WriteLine($"Type discovery report written to {outputPath}");
    }
    else
    {
        Console.WriteLine(typeJson);
    }

    return 0;
}

var options = new DbContextOptionsBuilder();
adapter.Configure(options, connectionString);
Console.WriteLine(JsonSerializer.Serialize(new AdapterLoadReport(
    adapter.Name,
    adapter.Version,
    adapter.GetType().FullName ?? adapter.GetType().Name,
    adapter.Capabilities.SupportedFeatures.Order(StringComparer.Ordinal).ToArray(),
    adapter.Capabilities.UnsupportedFeatures.Order(StringComparer.Ordinal).ToArray(),
    "Adapter configuration completed; no conformance scenarios were executed."), new JsonSerializerOptions { WriteIndented = true }));
return 0;

static IReadOnlyList<NativeTypeCoverageResult> BuildNativeTypeCoverage(
    TypeDiscoveryReport report,
    string testAssemblyPath,
    string providerName)
{
    if (!report.HasIndependentTypeInventory)
    {
        return Array.Empty<NativeTypeCoverageResult>();
    }

    var assembly = Assembly.LoadFrom(testAssemblyPath);
    var cases = new List<(string DatabaseTypeName, string ClrTypeName, string ContractId, string? ProviderName)>();
    foreach (var type in assembly.GetTypes())
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            foreach (var attribute in CustomAttributeData.GetCustomAttributes(method)
                         .Where(attribute => attribute.AttributeType.FullName == typeof(NativeTypeCoverageAttribute).FullName))
            {
                var args = attribute.ConstructorArguments;
                if (args.Count is 3 or 4 && args[0].Value is string databaseTypeName &&
                    args[1].Value is Type clrType && args[2].Value is string contractId)
                {
                    var declaredProviderName = args.Count == 4 && args[3].Value is string provider ? provider : null;
                    if (declaredProviderName is null || string.Equals(declaredProviderName, providerName, StringComparison.OrdinalIgnoreCase))
                    {
                        cases.Add((databaseTypeName, clrType.FullName ?? clrType.Name, contractId, declaredProviderName));
                    }
                }
            }
        }
    }

    var observedTypes = report.Types.Concat(report.SupplementalTypes.Where(type => type.IsIndependentDiscovery))
        .GroupBy(type => string.Join("\u001f",
            type.DatabaseTypeName.Trim(),
            type.ProviderTypeId ?? string.Empty,
            type.ClrTypeAssemblyQualifiedName ?? type.ClrTypeName ?? string.Empty,
            GetMetadataString(type, "CreateFormat")), StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .ToArray();
    var results = new List<NativeTypeCoverageResult>();
    foreach (var observedType in observedTypes)
    {
        var matches = cases.Where(testCase => string.Equals(
                testCase.DatabaseTypeName.Trim(), observedType.DatabaseTypeName.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var familyMatches = matches.Length > 0
            ? matches
            : cases.Where(testCase => string.Equals(
                    GetNativeTypeFamily(testCase.DatabaseTypeName),
                    GetNativeTypeFamily(observedType.DatabaseTypeName),
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
        results.Add(new NativeTypeCoverageResult(
            observedType.DatabaseTypeName.Trim(),
            matches.Length > 0 ? "LINKED" : familyMatches.Length > 0 ? "FAMILY_LINKED" : "UNLINKED",
            familyMatches.Select(item => item.ClrTypeName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            familyMatches.Select(item => item.ContractId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray())
        {
            ClrTypeName = observedType.ClrTypeName,
            ProviderTypeId = observedType.ProviderTypeId,
            CreateFormat = GetMetadataString(observedType, "CreateFormat")
        });
    }

    var observedTypeNames = observedTypes.Select(type => type.DatabaseTypeName.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var unmatchedCases = cases.Where(testCase => !observedTypeNames.Contains(testCase.DatabaseTypeName.Trim(), StringComparer.OrdinalIgnoreCase));
    results.AddRange(unmatchedCases
        .GroupBy(testCase => testCase.DatabaseTypeName.Trim(), StringComparer.OrdinalIgnoreCase)
        .Select(group => new NativeTypeCoverageResult(
            group.Key,
            "NOT_OBSERVED",
            group.Select(item => item.ClrTypeName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            group.Select(item => item.ContractId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray())));
    return results.OrderBy(item => item.DatabaseTypeName, StringComparer.OrdinalIgnoreCase).ToArray();
}

static string? GetMetadataString(DatabaseTypeDefinition type, string key) =>
    type.RawMetadata.TryGetValue(key, out var value)
        ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
        : null;

static TypeDiscoveryReport DiscoverTypeReport(DbConnection connection, IProviderAdapter adapter)
{
    var report = AdoNetTypeDiscovery.Discover(connection, adapter as ISupplementalTypeDiscovery);
    return adapter is IDatabaseTypeCatalogDiscovery catalogDiscovery
        ? report with
        {
            CatalogComparison = DatabaseTypeCatalogComparer.Compare(
                catalogDiscovery.DiscoverDatabaseTypeCatalog(connection),
                report.Types)
        }
        : report;
}

static string GetNativeTypeFamily(string databaseTypeName)
{
    var trimmed = databaseTypeName.Trim();
    var facetsStart = trimmed.IndexOf('(');
    return facetsStart < 0 ? trimmed : trimmed[..facetsStart].TrimEnd();
}

async Task<int> RunProfileTestsAsync(string[] arguments)
{
    if (arguments.Length is < 4 or > 5 || (arguments.Length == 5 && arguments[4] != "--cleanup-database"))
    {
        Console.Error.WriteLine("Usage: --run-profile <provider-profile.json> <test-project> <report-directory> [--cleanup-database]");
        return 2;
    }

    try
    {
        var profile = ProviderRunConfiguration.Load(arguments[1]);
        var runArguments = new List<string>
        {
            "--run",
            arguments[2],
            profile.AdapterAssembly,
            profile.AdapterType,
            arguments[3]
        };
        if (arguments.Length == 5)
        {
            runArguments.Add(arguments[4]);
        }

        Console.WriteLine($"Provider profile: {profile.Name}");
        return await RunTestsAsync(runArguments.ToArray(), profile.ResolveConnectionString());
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Provider profile error: {exception.Message}");
        return 2;
    }
}

async Task<int> RunTestsAsync(string[] arguments, string? configuredConnectionString = null)
{
    if (arguments.Length is < 5 or > 6 || (arguments.Length == 6 && arguments[5] != "--cleanup-database"))
    {
        Console.Error.WriteLine("Usage: --run <test-project> <adapter-assembly> <adapter-type> <report-directory> [--cleanup-database]");
        return 2;
    }

    var cs = configuredConnectionString ?? GetConnectionString();
    var project = Path.GetFullPath(arguments[1]);
    var adapterPath = Path.GetFullPath(arguments[2]);
    var adapterType = arguments[3];
    var reportDirectory = Path.GetFullPath(arguments[4]);
    var cleanupDatabase = arguments.Length == 6;
    Directory.CreateDirectory(reportDirectory);

    IProviderAdapter runAdapter;
    try
    {
        runAdapter = LoadAdapter(adapterPath, adapterType);
        runAdapter.Capabilities.Validate();
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"Adapter load error: {Sanitize(exception.ToString(), cs)}");
        return 2;
    }

    if (!runAdapter.Capabilities.SupportedFeatures.Contains("relational"))
    {
        var outcome = runAdapter.Capabilities.UnsupportedFeatures.Contains("relational")
            ? ConformanceOutcome.Unsupported
            : ConformanceOutcome.UndeclaredCapability;
        var required = new ConformanceResult("CAPABILITY-relational", "Relational provider support is required", outcome,
            "This runner only executes the relational EF Core profile.");
        await WriteReportsAsync(reportDirectory, runAdapter, project, [required]);
        return outcome == ConformanceOutcome.Unsupported ? 0 : 2;
    }

    var undeclared = runAdapter.Capabilities.UndeclaredFeatures;
    if (undeclared.Count > 0)
    {
        var capabilityResults = undeclared.Select(feature => new ConformanceResult(
            $"CAPABILITY-{feature}",
            $"Undeclared provider capability: {feature}",
            ConformanceOutcome.UndeclaredCapability,
            "Strict mode requires an explicit supported or unsupported declaration for every known capability.")).ToArray();
        await WriteReportsAsync(reportDirectory, runAdapter, project, capabilityResults);
        Console.Error.WriteLine($"Provider left {undeclared.Count} known capabilities undeclared; strict conformance run stopped.");
        return 2;
    }

    TypeDiscoveryReport? nativeDiscoveryReport = null;
    Exception? nativeDiscoveryException = null;
    if (runAdapter is IAdoNetProviderAdapter discoveryAdapter)
    {
        try
        {
            await using var discoveryConnection = discoveryAdapter.CreateConnection(cs);
            nativeDiscoveryReport = DiscoverTypeReport(discoveryConnection, runAdapter);
        }
        catch (Exception exception)
        {
            nativeDiscoveryException = exception;
        }
    }

    var trxPath = Path.Combine(reportDirectory, "conformance.trx");
    var logPath = Path.Combine(reportDirectory, "dotnet-test.log");
    var startInfo = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = Path.GetDirectoryName(project) ?? Environment.CurrentDirectory,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    };
    startInfo.ArgumentList.Add("test");
    startInfo.ArgumentList.Add(project);
    startInfo.ArgumentList.Add("--filter");
    startInfo.ArgumentList.Add("Category!=HarnessUnit");
    startInfo.ArgumentList.Add("--logger");
    startInfo.ArgumentList.Add("trx;LogFileName=conformance.trx");
    startInfo.ArgumentList.Add("--results-directory");
    startInfo.ArgumentList.Add(reportDirectory);
    startInfo.Environment["PENGDOWS_EFCORE_ADAPTER_ASSEMBLY"] = adapterPath;
    startInfo.Environment["PENGDOWS_EFCORE_ADAPTER_TYPE"] = adapterType;
    startInfo.Environment["PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING"] = cs;

    using var process = new Process { StartInfo = startInfo };
    string stdout;
    string stderr;
    try
    {
        if (!process.Start()) throw new InvalidOperationException("Could not start dotnet test.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        stdout = await stdoutTask;
        stderr = await stderrTask;
    }
    catch (Exception exception)
    {
        var error = new ConformanceResult("RUNNER-STARTUP", "Start provider test run", ConformanceOutcome.Error, Sanitize(exception.ToString(), cs), exception.GetType().FullName);
        await WriteReportsAsync(reportDirectory, runAdapter, project, [error]);
        return 2;
    }

    var combined = Sanitize(string.Join(Environment.NewLine, stdout, stderr), cs);
    await File.WriteAllTextAsync(logPath, combined);
    var testAssembly = GetTestAssemblyPath(project);
    var coverageByMethod = LoadTestCoverage(testAssembly);
    var capabilitiesByMethod = LoadCapabilityRequirements(testAssembly);
    var severityByMethod = LoadSeverityRequirements(testAssembly);
    var results = File.Exists(trxPath) ? ReadResults(trxPath, cs, coverageByMethod, capabilitiesByMethod, severityByMethod, runAdapter.Capabilities) : [];
    var nativeTypeReportPath = Path.Combine(reportDirectory, "ado-net-types.json");
    if (nativeDiscoveryException is not null)
    {
        results.Add(new ConformanceResult("RUNNER-TYPE-DISCOVERY", "Discover native type fidelity coverage", ConformanceOutcome.Error,
            Sanitize(nativeDiscoveryException.ToString(), cs), nativeDiscoveryException.GetType().FullName));
    }
    if (nativeDiscoveryReport is not null)
    {
        try
        {
            var typeReport = nativeDiscoveryReport;
            var testCases = runAdapter is INativeTypeMappingProfile nativeMappings
                ? nativeMappings.NativeTypeMappings
                    .SelectMany(mapping =>
                    {
                        var cases = new List<(NativeTypeMapping Mapping, string TestName, string ContractSuffix)>
                        {
                            (mapping, "DiscoveredNativeType_RoundTripsSynchronously", "ROUNDTRIP-SYNC"),
                            (mapping, "DiscoveredNativeType_RoundTripsAsynchronously", "ROUNDTRIP-ASYNC"),
                            (mapping, "DiscoveredNativeType_UpdatesAndRoundTripsSynchronously", "UPDATE-SYNC"),
                            (mapping, "DiscoveredNativeType_UpdatesAndRoundTripsAsynchronously", "UPDATE-ASYNC"),
                            (mapping, "DiscoveredNativeType_PreservesNullSynchronously", "NULL-SYNC"),
                            (mapping, "DiscoveredNativeType_PreservesNullAsynchronously", "NULL-ASYNC")
                        };
                        var clrType = Nullable.GetUnderlyingType(mapping.ClrType) ?? mapping.ClrType;
                        if (clrType == typeof(decimal))
                        {
                            cases.Add((mapping, "DiscoveredNativeType_DecimalBoundaryValuesRoundTripSynchronously", "DECIMAL-BOUNDARY-SYNC"));
                            cases.Add((mapping, "DiscoveredNativeType_DecimalBoundaryValuesRoundTripAsynchronously", "DECIMAL-BOUNDARY-ASYNC"));
                        }
                        if (!clrType.IsArray && clrType != typeof(byte[]) &&
                            !mapping.DatabaseTypeName.Contains("json", StringComparison.OrdinalIgnoreCase) &&
                            !mapping.DatabaseTypeName.Equals("xml", StringComparison.OrdinalIgnoreCase) &&
                            !mapping.DatabaseTypeName.Equals("point", StringComparison.OrdinalIgnoreCase) &&
                            !mapping.DatabaseTypeName.Equals("polygon", StringComparison.OrdinalIgnoreCase) &&
                            !mapping.DatabaseTypeName.Equals("cid", StringComparison.OrdinalIgnoreCase) &&
                            !mapping.DatabaseTypeName.Equals("xid8", StringComparison.OrdinalIgnoreCase))
                        {
                            cases.Add((mapping, "DiscoveredNativeType_QueriesByParameterizedValueSynchronously", "PARAMETER-SYNC"));
                            cases.Add((mapping, "DiscoveredNativeType_QueriesByParameterizedValueAsynchronously", "PARAMETER-ASYNC"));
                        }

                        return cases;
                    })
                    .ToArray()
                : [];
            var discoveredTypes = typeReport.Types
                .Concat(typeReport.SupplementalTypes.Where(type => type.IsIndependentDiscovery))
                .ToArray();
            var nativeCoverage = discoveredTypes
                .GroupBy(type => string.Join("\u001f", type.DatabaseTypeName, type.ProviderTypeId ?? "", type.ClrTypeAssemblyQualifiedName ?? type.ClrTypeName ?? ""), StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var type = group.First();
                    var mappings = testCases.Where(test => string.Equals(test.Mapping.DatabaseTypeName, type.DatabaseTypeName, StringComparison.OrdinalIgnoreCase) &&
                        (test.Mapping.ProviderTypeId is null || string.Equals(test.Mapping.ProviderTypeId, type.ProviderTypeId, StringComparison.OrdinalIgnoreCase)) &&
                        (test.Mapping.ClrType.FullName is null || type.ClrTypeName is null || string.Equals(test.Mapping.ClrType.FullName, type.ClrTypeName, StringComparison.Ordinal))).ToArray();
                    var contracts = mappings.Select(test => $"EF10-NATIVE-TYPE-{test.ContractSuffix}").Distinct(StringComparer.Ordinal).ToArray();
                    var caseResults = results.Where(result => mappings.Any(test => result.DisplayName.Contains(test.TestName, StringComparison.Ordinal)))
                        .Where(result => result.DisplayName.Contains($"databaseTypeName: \"{type.DatabaseTypeName}\"", StringComparison.OrdinalIgnoreCase))
                        .Where(result => mappings.Any(test => result.DisplayName.Contains(
                            $"clrTypeName: \"{test.Mapping.ClrType.Name}\"", StringComparison.Ordinal)))
                        .ToArray();
                    var outcome = mappings.Length == 0 ? "UNLINKED" : caseResults.Length == 0 ? "NOT_EXECUTED" :
                        caseResults.Any(result => result.Outcome is ConformanceOutcome.Fail or ConformanceOutcome.Error or ConformanceOutcome.Untranslatable) ? "FAIL" :
                        caseResults.All(result => result.Outcome == ConformanceOutcome.Pass) ? "PASS" : "PARTIAL";
                    return new NativeTypeCoverageResult(type.DatabaseTypeName, outcome,
                        mappings.Select(item => item.Mapping.ClrType.FullName ?? item.Mapping.ClrType.Name).Distinct(StringComparer.Ordinal).ToArray(), contracts)
                    {
                        ClrTypeName = type.ClrTypeName,
                        ProviderTypeId = type.ProviderTypeId,
                        CreateFormat = GetMetadataString(type, "CreateFormat")
                    };
                }).ToArray();
            await File.WriteAllTextAsync(nativeTypeReportPath, JsonSerializer.Serialize(typeReport with { NativeTypeCoverage = nativeCoverage }, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            results.Add(new ConformanceResult("RUNNER-TYPE-DISCOVERY", "Discover native type fidelity coverage", ConformanceOutcome.Error,
                Sanitize(exception.ToString(), cs), exception.GetType().FullName));
        }
    }
    if (File.Exists(trxPath))
    {
        var trxText = await File.ReadAllTextAsync(trxPath);
        await File.WriteAllTextAsync(trxPath, Sanitize(trxText, cs));
    }
    if (results.Count == 0)
    {
        results.Add(new ConformanceResult("RUNNER-NO-RESULTS", "Test host produced no test results", ConformanceOutcome.Error, $"See {Path.GetFileName(logPath)} for the test host output."));
    }

    if (cleanupDatabase)
    {
        try
        {
            await CleanupDatabaseAsync(runAdapter, cs);
        }
        catch (Exception exception)
        {
            results.Add(new ConformanceResult("RUNNER-CLEANUP", "Clean up disposable conformance database", ConformanceOutcome.Error,
                Sanitize(exception.ToString(), cs), exception.GetType().FullName));
        }
    }

    await WriteReportsAsync(reportDirectory, runAdapter, project, results);
    Console.WriteLine($"Provider: {runAdapter.Name} {runAdapter.Version}");
    Console.WriteLine($"Results: {results.Count} total; {results.Count(item => item.Outcome == ConformanceOutcome.Pass)} passed; {results.Count(item => item.Outcome is ConformanceOutcome.Fail or ConformanceOutcome.Untranslatable)} failed; {results.Count(item => item.Outcome == ConformanceOutcome.Unsupported)} unsupported; {results.Count(item => item.Outcome == ConformanceOutcome.UndeclaredCapability)} undeclared capabilities; {results.Count(item => item.Outcome == ConformanceOutcome.Error)} errors; {results.Count(item => item.Outcome is ConformanceOutcome.Skip or ConformanceOutcome.Inconclusive)} skipped/inconclusive.");
    Console.WriteLine($"Reports written to {reportDirectory}");
    return results.Any(item => item.Outcome == ConformanceOutcome.Error) && process.ExitCode == 0 ? 1 : process.ExitCode;
}

static async Task CleanupDatabaseAsync(IProviderAdapter adapter, string connectionString)
{
    try
    {
        await CleanupDatabaseOnceAsync(adapter, connectionString);
    }
    catch (Exception firstException)
    {
        // Some relational providers terminate the database connection while a successful
        // EnsureDeleted operation drops the database. A fresh context distinguishes that
        // expected disconnect from a cleanup that still needs to be retried.
        try
        {
            await CleanupDatabaseOnceAsync(adapter, connectionString);
        }
        catch (Exception retryException)
        {
            throw new AggregateException("Database cleanup failed on the initial attempt and retry.",
                firstException, retryException);
        }
    }
}

static async Task CleanupDatabaseOnceAsync(IProviderAdapter adapter, string connectionString)
{
    var options = new DbContextOptionsBuilder();
    adapter.Configure(options, connectionString);
    await using var context = new DbContext(options.Options);
    await adapter.CleanupDatabaseAsync(context, CancellationToken.None);
}

// Usage: --enumerate-cases <manifest.json> <cases.json>
// Phase 0 of the black-box harness: every public member in the manifest becomes a case with its driver and sync/async pair.
static async Task<int> EnumerateCasesAsync(string[] arguments)
{
    if (arguments.Length != 3)
    {
        Console.Error.WriteLine("Usage: --enumerate-cases <manifest.json> <cases.json>");
        return 2;
    }

    using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(arguments[1]));
    var cases = new List<object>();
    foreach (var entry in manifest.RootElement.GetProperty("Entries").EnumerateArray())
    {
        var disposition = entry.GetProperty("Disposition").GetString() ?? "";
        var declaringType = entry.GetProperty("DeclaringType").GetString() ?? "";
        var member = entry.GetProperty("ApiMemberName").ValueKind == JsonValueKind.String
            ? entry.GetProperty("ApiMemberName").GetString()!
            : entry.GetProperty("Signature").GetString()!;
        var kind = entry.GetProperty("MemberKind").GetString() ?? "";
        var signature = entry.GetProperty("Signature").GetString() ?? "";
        cases.Add(new
        {
            Id = $"{declaringType}.{member}#{cases.Count}",
            DeclaringType = declaringType,
            Member = member,
            MemberKind = kind,
            Signature = signature,
            Disposition = disposition,
            Driver = CaseEnumeration.DriverFor(declaringType),
            Exclusion = CaseEnumeration.ExclusionFor(declaringType)
                ?? (disposition is "excluded-ef-internal" or "ef-owned-no-provider-contract"
                    && !CaseEnumeration.IsExternalSurface(entry.GetProperty("DispositionReason").GetString() ?? "")
                    ? entry.GetProperty("DispositionReason").GetString()
                    : null)
                ?? (CaseEnumeration.DriverFor(declaringType) == "unassigned" ? CaseEnumeration.UnassignedExclusionFor(declaringType) : null),
            IsAsync = CaseEnumeration.IsAsync(member),
            SyncPair = CaseEnumeration.SyncNameOf(member),
        });
    }

    var summary = new
    {
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        Cases = cases.Count,
        ByDriver = cases.GroupBy(c => ((dynamic)c).Driver as string ?? "").ToDictionary(g => g.Key, g => g.Count()),
        Excluded = cases.Count(c => ((dynamic)c).Exclusion != null),
        ByDisposition = cases.GroupBy(c => ((dynamic)c).Disposition as string ?? "").ToDictionary(g => g.Key, g => g.Count()),
        Items = cases,
    };
    await File.WriteAllTextAsync(arguments[2], JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Wrote {cases.Count} cases to {arguments[2]}");
    return 0;
}

// Usage: --completeness <manifest.json> <output.json> <label>=<results.json> [...]
// Each API in the manifest gets a status per engine from the scenarios linked to it; pairs of engines then split the shared surface.
static async Task<int> CompletenessAsync(string[] arguments)
{
    if (arguments.Length < 4 || arguments[3..].Any(a => !a.Contains('=')))
    {
        Console.Error.WriteLine("Usage: --completeness <manifest.json> <output.json> <label>=<results.json> [...]");
        return 2;
    }

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    options.Converters.Add(new JsonStringEnumConverter());
    using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(arguments[1]));
    var entries = manifest.RootElement.GetProperty("Entries").EnumerateArray().ToList();

    var engines = new List<(string Label, Dictionary<string, ConformanceOutcome> Outcomes)>();
    foreach (var spec in arguments[3..])
    {
        var split = spec.IndexOf('=');
        var label = spec[..split];
        var report = JsonSerializer.Deserialize<ConformanceRunReport>(await File.ReadAllTextAsync(spec[(split + 1)..]), options);
        if (report is null)
        {
            Console.Error.WriteLine($"Could not read the results for '{label}'.");
            return 2;
        }

        engines.Add((label, AggregateOutcomes(report.Results)));
    }

    var rows = new List<object>();
    var statuses = engines.ToDictionary(e => e.Label, _ => new Dictionary<ApiStatus, int>());
    var engineStatuses = new List<Dictionary<string, ApiStatus>>();
    var dispositions = entries.GroupBy(e => e.GetProperty("Disposition").GetString() ?? "").ToDictionary(g => g.Key, g => g.Count());
    foreach (var entry in entries)
    {
        var contracts = entry.TryGetProperty("BehavioralContracts", out var list)
            ? list.EnumerateArray().Select(c => c.GetString()!).ToList()
            : new List<string>();
        var perEngine = new Dictionary<string, ApiStatus>();
        foreach (var (label, outcomes) in engines)
        {
            var status = CompletenessAnalysis.StatusOf(contracts, outcomes);
            perEngine[label] = status;
            statuses[label][status] = statuses[label].GetValueOrDefault(status) + 1;
        }

        if (contracts.Count > 0)
        {
            rows.Add(new
            {
                Signature = entry.GetProperty("Signature").GetString(),
                Assembly = entry.GetProperty("AssemblyName").GetString(),
                Statuses = perEngine.ToDictionary(kv => kv.Key, kv => kv.Value.ToString()),
            });
        }
    }

    var pairs = new List<object>();
    for (var i = 0; i < engines.Count; i++)
    {
        for (var j = i + 1; j < engines.Count; j++)
        {
            var counts = CompletenessAnalysis.Pair(rows.Cast<dynamic>().Select(r =>
                (ParseStatus(r.Statuses[engines[i].Label]), ParseStatus(r.Statuses[engines[j].Label]))));
            pairs.Add(new { A = engines[i].Label, B = engines[j].Label, counts.Both, OnlyA = counts.OnlyA, OnlyB = counts.OnlyB, counts.Neither, counts.Excluded });
        }
    }

    var summary = new
    {
        GeneratedAtUtc = DateTimeOffset.UtcNow,
        Entries = entries.Count,
        EntriesByDisposition = dispositions,
        Engines = statuses.ToDictionary(kv => kv.Key, kv => kv.Value.ToDictionary(s => s.Key.ToString(), s => s.Value)),
        Pairs = pairs,
        Apis = rows,
    };
    await File.WriteAllTextAsync(arguments[2], JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
    foreach (var pair in pairs)
    {
        Console.WriteLine(JsonSerializer.Serialize(pair));
    }

    return 0;
}

// Usage: --contract-gate <manifest.json> <test-assembly.dll> <ledger.json> <report.json> [provider-results.json ...]
// Reconciles the independently authored behavior inventory and upstream API inventory with executable test attributes.
static async Task<int> ContractGateAsync(string[] arguments)
{
    if (arguments.Length < 5)
    {
        Console.Error.WriteLine("Usage: --contract-gate <manifest.json> <test-assembly.dll> <ledger.json> <report.json> [provider-results.json ...]");
        return 2;
    }

    using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(arguments[1]));
    using var ledger = JsonDocument.Parse(await File.ReadAllTextAsync(arguments[3]));
    var assemblyPath = Path.GetFullPath(arguments[2]);
    if (!File.Exists(assemblyPath))
    {
        Console.Error.WriteLine($"Test assembly not found: {assemblyPath}");
        return 2;
    }

    using var peReader = new System.Reflection.PortableExecutable.PEReader(File.OpenRead(assemblyPath));
    var metadata = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(peReader);
    var testMethods = metadata.MethodDefinitions
        .Select(handle => (Handle: handle, Definition: metadata.GetMethodDefinition(handle)))
        .SelectMany(item => item.Definition.GetCustomAttributes()
            .Select(attributeHandle => metadata.GetCustomAttribute(attributeHandle))
            .Where(attribute => GetAttributeTypeName(metadata, attribute) is var name && (name == typeof(ConformanceContractAttribute).FullName || name == typeof(BehavioralCoverageAttribute).FullName))
            .SelectMany(attribute => ReadContractConstructorArguments(metadata, attribute)
                .Select(id => (Id: id,
                    Name: $"{GetDeclaringTypeName(metadata, item.Definition.GetDeclaringType())}.{metadata.GetString(item.Definition.Name)}"))))
        .Where(item => item.Id.Length > 0)
        .ToLookup(item => item.Id, item => item.Name, StringComparer.Ordinal);
    var testApiSelectors = metadata.MethodDefinitions
        .SelectMany(handle => metadata.GetMethodDefinition(handle).GetCustomAttributes()
            .Select(attributeHandle => metadata.GetCustomAttribute(attributeHandle))
            .Where(attribute => GetAttributeTypeName(metadata, attribute) == typeof(BehavioralCoverageAttribute).FullName)
            .SelectMany(attribute => ReadBehavioralApiSelectors(metadata, attribute)))
        .ToHashSet(StringComparer.Ordinal);
    foreach (var signature in CollectInvokedApiSignatures(assemblyPath, manifest.RootElement))
    {
        testApiSelectors.Add(signature);
    }

    var behaviorDefinitions = ledger.RootElement.GetProperty("behaviorContracts").EnumerateArray()
        .Select(item => (Id: item.GetProperty("id").GetString()!, Status: item.GetProperty("status").GetString()!, LinkedContracts: item.TryGetProperty("testContractIds", out var linked) ? linked.EnumerateArray().Select(value => value.GetString() ?? "").ToArray() : []))
        .ToList();
    var resultOutcomes = new Dictionary<string, Dictionary<string, List<ConformanceOutcome>>>(StringComparer.Ordinal);
    foreach (var resultPath in arguments[5..])
    {
        var resultReport = JsonSerializer.Deserialize<ConformanceRunReport>(await File.ReadAllTextAsync(resultPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } });
        if (resultReport is null) continue;
        if (!resultOutcomes.TryGetValue(resultReport.Provider, out var providerResults)) resultOutcomes.Add(resultReport.Provider, providerResults = new Dictionary<string, List<ConformanceOutcome>>(StringComparer.Ordinal));
        foreach (var result in resultReport.Results)
        {
            if (!providerResults.TryGetValue(result.ContractId, out var values)) providerResults.Add(result.ContractId, values = []);
            values.Add(result.Outcome);
        }
    }
    var apiContractIds = manifest.RootElement.GetProperty("Entries").EnumerateArray()
        .Where(entry => entry.TryGetProperty("BehavioralContracts", out _))
        .SelectMany(entry => entry.GetProperty("BehavioralContracts").EnumerateArray().Select(value => value.GetString() ?? ""))
        .Where(id => id.Length > 0)
        .Distinct(StringComparer.Ordinal)
        .ToHashSet(StringComparer.Ordinal);
    var apiSelectorCounts = manifest.RootElement.GetProperty("Entries").EnumerateArray()
        .Where(entry => entry.TryGetProperty("Disposition", out var disposition) && disposition.GetString() == "behavior-tested-family")
        .Select(entry => entry.TryGetProperty("ApiSelector", out var selector) ? selector.GetString() : null)
        .Where(selector => !string.IsNullOrWhiteSpace(selector))
        .GroupBy(selector => selector!, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
    var behaviors = behaviorDefinitions.Select(item =>
    {
        var linked = item.LinkedContracts.Length > 0 && item.LinkedContracts.All(id => testMethods[id].Any());
        var providerResults = resultOutcomes.ToDictionary(
            provider => provider.Key,
            provider =>
            {
                var outcomes = item.LinkedContracts.SelectMany(id => provider.Value.GetValueOrDefault(id) ?? []).ToArray();
                return new { Outcomes = outcomes, ResultStatus = ResultStatusFor(outcomes) };
            },
            StringComparer.Ordinal);
        var observed = linked && resultOutcomes.Count > 0 && resultOutcomes.Values.All(provider => item.LinkedContracts.All(provider.ContainsKey));
        return new
        {
            item.Id,
            InventoryStatus = item.Status,
            TestContractIds = item.LinkedContracts,
            Tests = item.LinkedContracts.SelectMany(id => testMethods[id]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            ProviderResults = providerResults,
            // A few linked test methods do not complete an inventory row marked PARTIAL/NOT_TESTED.
            // Report execution evidence separately from inventory completeness.
            GateStatus = item.Status.Equals("COMPLETE", StringComparison.OrdinalIgnoreCase)
                ? observed ? "COMPLETE" : "NOT_TESTED"
                : observed ? "COVERAGE_PARTIAL" : "NOT_TESTED",
        };
    }).ToList();

    var apiRows = new List<object>();
    var unclassified = 0;
    var uncovered = 0;
    foreach (var entry in manifest.RootElement.GetProperty("Entries").EnumerateArray())
    {
        var signature = entry.TryGetProperty("Signature", out var signatureElement) ? signatureElement.GetString() ?? "" : "";
        var disposition = entry.TryGetProperty("Disposition", out var dispositionElement) ? dispositionElement.GetString() ?? "unclassified" : "unclassified";
        var rationale = entry.TryGetProperty("DispositionReason", out var reasonElement) ? reasonElement.GetString() : null;
        var linkedContracts = entry.TryGetProperty("BehavioralContracts", out var contractList)
            ? contractList.EnumerateArray().Select(value => value.GetString() ?? "").Where(value => value.Length > 0).ToArray()
            : [];
        var linkedTests = linkedContracts.SelectMany(id => testMethods[id]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var excluded = disposition is "not-applicable-with-rationale" or "ef-owned-no-provider-contract" or "excluded-ef-internal" or "excluded-obsolete-error"
            || entry.TryGetProperty("ProviderApplicable", out var applicable) && applicable.ValueKind == JsonValueKind.False;
        var apiSelector = entry.TryGetProperty("ApiSelector", out var apiSelectorElement) ? apiSelectorElement.GetString() : null;
        var selectorCount = apiSelector is not null ? apiSelectorCounts.GetValueOrDefault(apiSelector) : 0;
        var exactApiSelectorLinked = !excluded && (testApiSelectors.Contains(signature)
            || apiSelector is not null && testApiSelectors.Contains(apiSelector) && selectorCount == 1);
        var overloadSelectorLinked = !excluded && apiSelector is not null && testApiSelectors.Contains(apiSelector) && selectorCount > 1;
        var isUnclassified = disposition is "unclassified" or "capability-gated" && string.IsNullOrWhiteSpace(rationale);
        // BehavioralContracts are API-family references. A unique API selector on an
        // executable attributed test links a singleton member; duplicate selectors still
        // need overload-specific evidence.
        var isUncovered = !excluded && !exactApiSelectorLinked;
        if (isUnclassified) unclassified++;
        if (isUncovered) uncovered++;
        var resultStatusByProvider = resultOutcomes.ToDictionary(
            provider => provider.Key,
            provider => ResultStatusFor(linkedContracts.SelectMany(id => provider.Value.GetValueOrDefault(id) ?? [])),
            StringComparer.Ordinal);
        apiRows.Add(new { signature, disposition, rationale, linkedContracts, tests = linkedTests, resultStatusByProvider, status = isUnclassified ? "UNCLASSIFIED" : excluded ? "EXCLUDED_WITH_RATIONALE" : exactApiSelectorLinked ? "API_SPECIFIC_SELECTOR_LINKED" : overloadSelectorLinked ? "OVERLOADS_UNVERIFIED" : linkedTests.Length == 0 ? "NOT_TESTED" : "FAMILY_LINKED_API_UNVERIFIED" });
    }

    var providerSummaries = resultOutcomes.Keys.ToDictionary(
        provider => provider,
        provider => new
        {
            behaviorPass = behaviors.Count(row => row.ProviderResults[provider].ResultStatus == "PASS"),
            behaviorFail = behaviors.Count(row => row.ProviderResults[provider].ResultStatus == "FAIL"),
            behaviorUnsupported = behaviors.Count(row => row.ProviderResults[provider].ResultStatus == "UNSUPPORTED"),
            behaviorBlocked = behaviors.Count(row => row.ProviderResults[provider].ResultStatus == "BLOCKED"),
            apiPass = apiRows.Count(row => ((dynamic)row).resultStatusByProvider[provider] == "PASS"),
            apiFail = apiRows.Count(row => ((dynamic)row).resultStatusByProvider[provider] == "FAIL"),
            apiUnsupported = apiRows.Count(row => ((dynamic)row).resultStatusByProvider[provider] == "UNSUPPORTED"),
            apiBlocked = apiRows.Count(row => ((dynamic)row).resultStatusByProvider[provider] == "BLOCKED"),
            apiNotTestedApplicable = apiRows.Count(row => ((dynamic)row).resultStatusByProvider[provider] == "NOT_TESTED" && ((dynamic)row).status != "EXCLUDED_WITH_RATIONALE"),
        },
        StringComparer.Ordinal);

    var defectDetectionControls = ledger.RootElement.GetProperty("defectDetectionControls").EnumerateArray()
        .Select(control => new
        {
            Id = control.GetProperty("id").GetString() ?? "",
            Result = control.TryGetProperty("result", out var result) ? result.GetString() ?? "NOT_TESTED" : "NOT_TESTED",
        }).ToArray();
    var completionGate = new
    {
        zeroUnclassifiedApis = unclassified == 0,
        zeroUncoveredApplicableApis = uncovered == 0,
        everyBehaviorContractComplete = behaviors.All(row => row.GateStatus == "COMPLETE"),
        independentProviderMinimumMet = resultOutcomes.Count >= 2,
        defectDetectionControlPassed = defectDetectionControls.Length > 0 && defectDetectionControls.All(control => control.Result == "PASS"),
    };
    var completion = completionGate.zeroUnclassifiedApis
        && completionGate.zeroUncoveredApplicableApis
        && completionGate.everyBehaviorContractComplete
        && completionGate.independentProviderMinimumMet
        && completionGate.defectDetectionControlPassed
            ? "COMPLETE"
            : "INCOMPLETE";

    var report = new
    {
        generatedAtUtc = DateTimeOffset.UtcNow,
        source = new { manifest = Path.GetFullPath(arguments[1]), testAssembly = assemblyPath, ledger = Path.GetFullPath(arguments[3]) },
        summary = new { upstreamApiEntries = apiRows.Count, unclassifiedApis = unclassified, uncoveredApis = uncovered, apiSpecificSelectorLinked = apiRows.Count(row => ((dynamic)row).status == "API_SPECIFIC_SELECTOR_LINKED"), overloadsUnverified = apiRows.Count(row => ((dynamic)row).status == "OVERLOADS_UNVERIFIED"), apiFamilyLinkedUnverified = apiRows.Count(row => ((dynamic)row).status == "FAMILY_LINKED_API_UNVERIFIED"), apiBehaviorContracts = apiContractIds.Count, linkedApiBehaviorContracts = apiContractIds.Count(id => testMethods[id].Any()), behaviorContracts = behaviors.Count, untestedBehaviorContracts = behaviors.Count(row => row.GateStatus == "NOT_TESTED"), partialBehaviorContracts = behaviors.Count(row => row.GateStatus == "COVERAGE_PARTIAL"), completeBehaviorContracts = behaviors.Count(row => row.GateStatus == "COMPLETE"), apiExcluded = apiRows.Count(row => ((dynamic)row).status == "EXCLUDED_WITH_RATIONALE"), attributedTestMethods = testMethods.SelectMany(group => group).Distinct(StringComparer.Ordinal).Count(), providerReports = arguments.Length - 5, providers = providerSummaries },
        completion,
        completionGate,
        behaviorContracts = behaviors,
        apiContracts = apiRows,
        providerValidation = ledger.RootElement.GetProperty("providerValidation").Clone(),
        defectDetectionControls,
    };
    var output = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(Path.GetFullPath(arguments[4]), output);
    Console.WriteLine(JsonSerializer.Serialize(report.summary));
    return 0;
}

static HashSet<string> CollectInvokedApiSignatures(string testAssemblyPath, JsonElement inventory)
{
    var tokenToSignature = inventory.GetProperty("Entries").EnumerateArray()
        .Where(entry => entry.GetProperty("MemberKind").GetString() == "Method"
            && entry.GetProperty("Disposition").GetString() == "behavior-tested-family"
            && entry.TryGetProperty("ProviderApplicable", out var applicable)
            && applicable.ValueKind == JsonValueKind.True
            && entry.TryGetProperty("MetadataToken", out var methodToken)
            && methodToken.GetInt32() != 0
            && entry.TryGetProperty("ModuleVersionId", out _))
        .ToDictionary(
            entry => $"{entry.GetProperty("AssemblyName").GetString()}|{entry.GetProperty("ModuleVersionId").GetGuid():D}|{entry.GetProperty("MetadataToken").GetInt32()}",
            entry => entry.GetProperty("Signature").GetString() ?? "",
            StringComparer.Ordinal);
    var signatures = new HashSet<string>(StringComparer.Ordinal);
    var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(opcode => unchecked((ushort)opcode.Value));
    var loadContext = new ApiTestAssemblyLoadContext(testAssemblyPath);
    try
    {
        var assembly = loadContext.LoadFromAssemblyPath(testAssemblyPath);
        foreach (var type in assembly.GetTypes())
        {
            foreach (var testMethod in type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (!CustomAttributeData.GetCustomAttributes(testMethod).Any(attribute => attribute.AttributeType.FullName == typeof(BehavioralCoverageAttribute).FullName))
                    continue;
                var il = testMethod.GetMethodBody()?.GetILAsByteArray();
                if (il is null) continue;
                var position = 0;
                while (position < il.Length)
                {
                    var opcodeByte = il[position++];
                    var opcodeValue = opcodeByte == 0xFE ? (ushort)(0xFE00 | il[position++]) : opcodeByte;
                    if (!opcodes.TryGetValue(opcodeValue, out var opcode))
                        throw new BadImageFormatException($"Unknown IL opcode 0x{opcodeValue:X4} in {testMethod.DeclaringType?.FullName}.{testMethod.Name}.");
                    if (opcode.OperandType == OperandType.InlineMethod)
                    {
                        var token = BitConverter.ToInt32(il, position);
                        try
                        {
                            var target = testMethod.Module.ResolveMethod(token, testMethod.DeclaringType?.GetGenericArguments(), testMethod.IsGenericMethod ? testMethod.GetGenericArguments() : null);
                            if (target is not null)
                            {
                                var definition = target is MethodInfo methodInfo && methodInfo.IsGenericMethod
                                    ? methodInfo.GetGenericMethodDefinition()
                                    : target;
                                var key = $"{definition.Module.Assembly.GetName().Name}|{definition.Module.ModuleVersionId:D}|{definition.MetadataToken}";
                                if (tokenToSignature.TryGetValue(key, out var signature)) signatures.Add(signature);
                            }
                        }
                        catch (ArgumentException)
                        {
                            // Some method tokens reference generic contexts that cannot be resolved outside execution.
                        }
                    }
                    position += OperandSize(opcode.OperandType, il, position);
                }
            }
        }
    }
    finally
    {
        loadContext.Unload();
    }
    return signatures;
}

static int OperandSize(OperandType operandType, byte[] il, int position) => operandType switch
{
    OperandType.InlineNone => 0,
    OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
    OperandType.InlineVar => 2,
    OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
    OperandType.InlineI8 or OperandType.InlineR => 8,
    OperandType.InlineSwitch => checked(4 + BitConverter.ToInt32(il, position) * 4),
    _ => throw new BadImageFormatException($"Unsupported IL operand type '{operandType}'."),
};

static string ResultStatusFor(IEnumerable<ConformanceOutcome> outcomes)
{
    var values = outcomes.ToArray();
    if (values.Length == 0) return "NOT_TESTED";
    if (values.Any(outcome => outcome is ConformanceOutcome.Fail or ConformanceOutcome.Error)) return "FAIL";
    if (values.Any(outcome => outcome is ConformanceOutcome.UndeclaredCapability or ConformanceOutcome.Inconclusive)) return "BLOCKED";
    if (values.Any(outcome => outcome is ConformanceOutcome.Unsupported or ConformanceOutcome.Skip or ConformanceOutcome.Untranslatable)) return "UNSUPPORTED";
    return values.All(outcome => outcome == ConformanceOutcome.Pass) ? "PASS" : "NOT_TESTED";
}

static string GetAttributeTypeName(System.Reflection.Metadata.MetadataReader metadata, System.Reflection.Metadata.CustomAttribute attribute)
{
    var constructor = attribute.Constructor;
    System.Reflection.Metadata.EntityHandle parent = constructor.Kind switch
    {
        System.Reflection.Metadata.HandleKind.MethodDefinition => metadata.GetMethodDefinition((System.Reflection.Metadata.MethodDefinitionHandle)constructor).GetDeclaringType(),
        System.Reflection.Metadata.HandleKind.MemberReference => metadata.GetMemberReference((System.Reflection.Metadata.MemberReferenceHandle)constructor).Parent,
        _ => default,
    };
    return GetTypeName(metadata, parent);
}

static string GetTypeName(System.Reflection.Metadata.MetadataReader metadata, System.Reflection.Metadata.EntityHandle handle)
{
    return handle.Kind switch
    {
        System.Reflection.Metadata.HandleKind.TypeDefinition => GetDefinitionName(metadata, (System.Reflection.Metadata.TypeDefinitionHandle)handle),
        System.Reflection.Metadata.HandleKind.TypeReference => GetReferenceName(metadata, (System.Reflection.Metadata.TypeReferenceHandle)handle),
        _ => "",
    };
}

static string GetDefinitionName(System.Reflection.Metadata.MetadataReader metadata, System.Reflection.Metadata.TypeDefinitionHandle handle)
{
    var definition = metadata.GetTypeDefinition(handle);
    var ns = metadata.GetString(definition.Namespace);
    var name = metadata.GetString(definition.Name);
    return ns.Length == 0 ? name : $"{ns}.{name}";
}

static string GetReferenceName(System.Reflection.Metadata.MetadataReader metadata, System.Reflection.Metadata.TypeReferenceHandle handle)
{
    var reference = metadata.GetTypeReference(handle);
    var ns = metadata.GetString(reference.Namespace);
    var name = metadata.GetString(reference.Name);
    return ns.Length == 0 ? name : $"{ns}.{name}";
}

static string GetDeclaringTypeName(System.Reflection.Metadata.MetadataReader metadata, System.Reflection.Metadata.TypeDefinitionHandle handle)
    => GetDefinitionName(metadata, handle);

static IEnumerable<string> ReadBehavioralApiSelectors(System.Reflection.Metadata.MetadataReader metadata, System.Reflection.Metadata.CustomAttribute attribute)
{
    var reader = metadata.GetBlobReader(attribute.Value);
    if (reader.ReadUInt16() != 1 || string.IsNullOrWhiteSpace(reader.ReadSerializedString())) return [];
    var count = reader.ReadInt32();
    if (count is < 0 or > 1000) return [];
    var selectors = new List<string>(count);
    for (var index = 0; index < count && reader.RemainingBytes > 0; index++)
    {
        var selector = reader.ReadSerializedString();
        if (!string.IsNullOrWhiteSpace(selector)) selectors.Add(selector);
    }

    return selectors;
}

static IEnumerable<string> ReadContractConstructorArguments(System.Reflection.Metadata.MetadataReader metadata, System.Reflection.Metadata.CustomAttribute attribute)
{
    var name = GetAttributeTypeName(metadata, attribute);
    var reader = metadata.GetBlobReader(attribute.Value);
    if (reader.ReadUInt16() != 1)
    {
        return [];
    }

    var first = reader.ReadSerializedString();
    if (string.IsNullOrWhiteSpace(first))
    {
        return [];
    }

    if (name == typeof(ConformanceContractAttribute).FullName)
    {
        return [first];
    }

    var count = reader.ReadInt32();
    if (count is < 0 or > 1000)
    {
        return [first];
    }

    var contracts = new List<string> { first };
    for (var index = 0; index < count; index++)
    {
        if (reader.RemainingBytes == 0) break;
        var selector = reader.ReadSerializedString();
        // BehavioralCoverage's API selectors follow its first contract ID; only the first constructor argument is a test ID.
        _ = selector;
    }

    return contracts;
}

static ApiStatus ParseStatus(object? value)
    => Enum.Parse<ApiStatus>(value?.ToString() ?? nameof(ApiStatus.Unknown));

// One status per scenario: any unknown-type outcome makes it unknown, then any declared-unsupported or skipped one, then Untranslatable.
static Dictionary<string, ConformanceOutcome> AggregateOutcomes(IEnumerable<ConformanceResult> results)
{
    var byId = new Dictionary<string, ConformanceOutcome>(StringComparer.Ordinal);
    foreach (var group in results.GroupBy(r => r.ContractId, StringComparer.Ordinal))
    {
        var outcomes = group.Select(r => r.Outcome).ToList();
        byId[group.Key] = outcomes.Any(o => o is ConformanceOutcome.Error or ConformanceOutcome.Inconclusive or ConformanceOutcome.UndeclaredCapability)
            ? outcomes.First(o => o is ConformanceOutcome.Error or ConformanceOutcome.Inconclusive or ConformanceOutcome.UndeclaredCapability)
            : outcomes.Any(o => o is ConformanceOutcome.Unsupported or ConformanceOutcome.Skip)
                ? ConformanceOutcome.Unsupported
                : outcomes.Any(o => o == ConformanceOutcome.Untranslatable)
                    ? ConformanceOutcome.Untranslatable
                    : ConformanceOutcome.Pass;
    }

    return byId;
}

static async Task<int> CompareReportsAsync(string[] arguments)
{
    if (arguments.Length != 4)
    {
        Console.Error.WriteLine("Usage: --compare <candidate-results.json> <reference-results.json> <comparison.json>");
        return 2;
    }

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    options.Converters.Add(new JsonStringEnumConverter());
    var candidate = JsonSerializer.Deserialize<ConformanceRunReport>(await File.ReadAllTextAsync(arguments[1]), options);
    var reference = JsonSerializer.Deserialize<ConformanceRunReport>(await File.ReadAllTextAsync(arguments[2]), options);
    if (candidate is null || reference is null)
    {
        Console.Error.WriteLine("Could not read one of the conformance result reports.");
        return 2;
    }

    if (!string.Equals(candidate.EfCoreVersion, reference.EfCoreVersion, StringComparison.Ordinal) ||
        !string.Equals(candidate.TestProject, reference.TestProject, StringComparison.Ordinal))
    {
        Console.Error.WriteLine("Comparison requires reports from the same EF Core version and test project.");
        return 2;
    }

    var candidateByName = candidate.Results.ToDictionary(result => result.DisplayName, StringComparer.Ordinal);
    var referenceByName = reference.Results.ToDictionary(result => result.DisplayName, StringComparer.Ordinal);
    var allNames = candidateByName.Keys.Concat(referenceByName.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
    var comparisons = allNames.Select(name =>
    {
        candidateByName.TryGetValue(name, out var candidateResult);
        referenceByName.TryGetValue(name, out var referenceResult);
        var classification = ClassifyComparison(candidateResult, referenceResult);
        return new ConformanceComparisonResult(
            candidateResult?.ContractId ?? referenceResult?.ContractId ?? name,
            name,
            candidateResult?.Outcome,
            referenceResult?.Outcome,
            classification,
            candidateResult?.Reason,
            referenceResult?.Reason);
    }).ToArray();
    var report = new ConformanceComparisonReport(
        DateTimeOffset.UtcNow,
        candidate.EfCoreVersion,
        candidate.Provider,
        reference.Provider,
        comparisons.Length,
        comparisons.Count(item => item.Classification == "PASS_ON_BOTH"),
        comparisons.Count(item => item.Classification == "CANDIDATE_ONLY_FAILURE"),
        comparisons.Count(item => item.Classification == "FAILS_ON_REFERENCE_TOO"),
        comparisons);
    var outputPath = Path.GetFullPath(arguments[3]);
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await using var output = File.Create(outputPath);
    await JsonSerializer.SerializeAsync(output, report, options);
    Console.WriteLine($"Compared {report.Total} cases: {report.PassedOnBoth} pass on both; {report.CandidateOnlyFailures} fail only on candidate; {report.FailuresAlsoOnReference} fail on reference too.");
    Console.WriteLine($"Comparison written to {outputPath}");
    return 0;
}

static string ClassifyComparison(ConformanceResult? candidate, ConformanceResult? reference)
{
    if (candidate is null) return "MISSING_FROM_CANDIDATE";
    if (reference is null) return "MISSING_FROM_REFERENCE";
    var candidateFailed = candidate.Outcome is ConformanceOutcome.Fail or ConformanceOutcome.Untranslatable;
    var referenceFailed = reference.Outcome is ConformanceOutcome.Fail or ConformanceOutcome.Untranslatable;
    if (!candidateFailed && !referenceFailed && candidate.Outcome == ConformanceOutcome.Pass && reference.Outcome == ConformanceOutcome.Pass) return "PASS_ON_BOTH";
    if (candidateFailed && referenceFailed) return "FAILS_ON_REFERENCE_TOO";
    if (candidateFailed && reference.Outcome == ConformanceOutcome.Pass) return "CANDIDATE_ONLY_FAILURE";
    if (candidate.Outcome == ConformanceOutcome.Pass && referenceFailed) return "REFERENCE_ONLY_FAILURE";
    if (candidate.Outcome == ConformanceOutcome.Unsupported && reference.Outcome == ConformanceOutcome.Unsupported) return "UNSUPPORTED_ON_BOTH";
    if (candidate.Outcome == ConformanceOutcome.Unsupported || reference.Outcome == ConformanceOutcome.Unsupported) return "CAPABILITY_OUTCOME_DIFFERS";
    return "OUTCOME_DIFFERS";
}

// Usage: --verify-defect-control <correct-results.json> <defective-results.json> <output.json> <contract-id> [...]
// A control passes only when all selected contracts pass on the correct adapter and fail on the mutant.
static async Task<int> VerifyDefectControlAsync(string[] arguments)
{
    if (arguments.Length < 5)
    {
        Console.Error.WriteLine("Usage: --verify-defect-control <correct-results.json> <defective-results.json> <output.json> <contract-id> [...]");
        return 2;
    }

    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new JsonStringEnumConverter() } };
    var correct = JsonSerializer.Deserialize<ConformanceRunReport>(await File.ReadAllTextAsync(arguments[1]), options);
    var defective = JsonSerializer.Deserialize<ConformanceRunReport>(await File.ReadAllTextAsync(arguments[2]), options);
    if (correct is null || defective is null)
    {
        Console.Error.WriteLine("Could not read a complete provider result report.");
        return 2;
    }

    var contracts = arguments[4..].Distinct(StringComparer.Ordinal).Select(id =>
    {
        var correctOutcomes = correct.Results.Where(result => result.ContractId == id).Select(result => result.Outcome).ToArray();
        var defectiveOutcomes = defective.Results.Where(result => result.ContractId == id).Select(result => result.Outcome).ToArray();
        var passed = correctOutcomes.Length > 0 && defectiveOutcomes.Length > 0
            && correctOutcomes.All(outcome => outcome == ConformanceOutcome.Pass)
            && defectiveOutcomes.All(outcome => outcome == ConformanceOutcome.Fail);
        return new { contractId = id, correctOutcomes, defectiveOutcomes, result = passed ? "PASS" : "FAIL" };
    }).ToArray();
    var controlResult = contracts.Length > 0 && contracts.All(contract => contract.result == "PASS") ? "PASS" : "FAIL";
    var report = new
    {
        controlId = "DEFECTIVE-PROVIDER-POSITIVE-CONTROL",
        correctProvider = correct.Provider,
        defectiveProvider = defective.Provider,
        correctResults = Path.GetFullPath(arguments[1]),
        defectiveResults = Path.GetFullPath(arguments[2]),
        contracts,
        result = controlResult,
    };
    var outputPath = Path.GetFullPath(arguments[3]);
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine(JsonSerializer.Serialize(report));
    return controlResult == "PASS" ? 0 : 1;
}

static string GetConnectionString() =>
    Environment.GetEnvironmentVariable("PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING") is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException("PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING is required.");

static IProviderAdapter LoadAdapter(string assemblyPath, string typeName)
    => ProviderAdapterLoader.Load(assemblyPath, typeName);

static List<ConformanceResult> ReadResults(
    string trxPath,
    string connectionString,
    IReadOnlyDictionary<string, HashSet<string>> coverageByMethod,
    IReadOnlyDictionary<string, HashSet<string>> capabilitiesByMethod,
    IReadOnlyDictionary<string, ConformanceSeverity> severityByMethod,
    ProviderCapabilities capabilities)
{
    var document = XDocument.Load(trxPath);
    var ns = document.Root?.Name.Namespace ?? XNamespace.None;
    return document.Descendants(ns + "UnitTestResult").Select(result =>
    {
        var outcomeText = (string?)result.Attribute("outcome") ?? "Failed";
        var name = (string?)result.Attribute("testName") ?? "Unnamed test";
        var errorInfo = result.Descendants(ns + "ErrorInfo").FirstOrDefault();
        var message = errorInfo?.Element(ns + "Message")?.Value;
        var separator = message?.IndexOf(" :", StringComparison.Ordinal) ?? -1;
        var exceptionType = errorInfo?.Element(ns + "ErrorType")?.Value
                            ?? (separator > 0 ? message![..separator] : null);
        var methodKey = ExtractMethodKey(name);
        var unsupportedByDeclaration = capabilitiesByMethod.TryGetValue(methodKey, out var requiredFeatures) &&
                                       requiredFeatures.Any(feature => capabilities.UnsupportedFeatures.Contains(feature));
        var outcome = outcomeText.ToUpperInvariant() switch
        {
            "PASSED" => ConformanceOutcome.Pass,
            "NOTEXECUTED" when unsupportedByDeclaration => ConformanceOutcome.Unsupported,
            "NOTEXECUTED" or "INCONCLUSIVE" => ConformanceOutcome.Skip,
            _ when exceptionType?.EndsWith(nameof(UndeclaredCapabilityException), StringComparison.Ordinal) == true => ConformanceOutcome.UndeclaredCapability,
            _ when exceptionType?.EndsWith(nameof(ConformanceInfrastructureException), StringComparison.Ordinal) == true => ConformanceOutcome.Error,
            _ when exceptionType?.EndsWith(nameof(InvalidOperationException), StringComparison.Ordinal) == true &&
                   message?.Contains("could not be translated", StringComparison.OrdinalIgnoreCase) == true => ConformanceOutcome.Untranslatable,
            _ => ConformanceOutcome.Fail
        };
        var contractId = coverageByMethod.TryGetValue(methodKey, out var contracts)
            ? string.Join(",", contracts.Order(StringComparer.Ordinal))
            : name;
        var severity = severityByMethod.TryGetValue(methodKey, out var configuredSeverity) ? configuredSeverity : ConformanceSeverity.Hard;
        return new ConformanceResult(contractId, name, outcome, message is null ? null : Sanitize(message, connectionString), exceptionType, severity);
    }).ToList();
}

static Dictionary<string, HashSet<string>> LoadCapabilityRequirements(string assemblyPath)
{
    var requirements = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    if (!File.Exists(assemblyPath)) return requirements;
    var assembly = Assembly.LoadFrom(assemblyPath);
    foreach (var type in assembly.GetTypes())
    {
        var typeFeatures = CustomAttributeData.GetCustomAttributes(type)
            .Where(attribute => attribute.AttributeType == typeof(CapabilityRequirementAttribute))
            .Select(attribute => (string)attribute.ConstructorArguments[0].Value!)
            .ToArray();
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var features = typeFeatures.Concat(CustomAttributeData.GetCustomAttributes(method)
                    .Where(attribute => attribute.AttributeType == typeof(CapabilityRequirementAttribute))
                    .Select(attribute => (string)attribute.ConstructorArguments[0].Value!))
                .ToHashSet(StringComparer.Ordinal);
            if (features.Count > 0) requirements[$"{type.FullName}.{method.Name}"] = features;
        }
    }

    return requirements;
}

static Dictionary<string, ConformanceSeverity> LoadSeverityRequirements(string assemblyPath)
{
    var severities = new Dictionary<string, ConformanceSeverity>(StringComparer.Ordinal);
    if (!File.Exists(assemblyPath)) return severities;
    var assembly = Assembly.LoadFrom(assemblyPath);
    foreach (var type in assembly.GetTypes())
    {
        var typeSeverity = CustomAttributeData.GetCustomAttributes(type)
            .FirstOrDefault(attribute => attribute.AttributeType == typeof(ConformanceSeverityAttribute))
            ?.ConstructorArguments[0].Value;
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            var methodSeverity = CustomAttributeData.GetCustomAttributes(method)
                .FirstOrDefault(attribute => attribute.AttributeType == typeof(ConformanceSeverityAttribute))
                ?.ConstructorArguments[0].Value;
            var value = methodSeverity ?? typeSeverity;
            if (value is not null)
            {
                severities[$"{type.FullName}.{method.Name}"] = (ConformanceSeverity)value;
            }
        }
    }

    return severities;
}

static string GetTestAssemblyPath(string projectPath)
{
    var project = XDocument.Load(projectPath);
    var assemblyName = project.Descendants().FirstOrDefault(element => element.Name.LocalName == "AssemblyName")?.Value
                       ?? Path.GetFileNameWithoutExtension(projectPath);
    var targetFramework = project.Descendants().FirstOrDefault(element => element.Name.LocalName == "TargetFramework")?.Value
                          ?? "net10.0";
    return Path.Combine(Path.GetDirectoryName(projectPath)!, "bin", "Debug", targetFramework, assemblyName + ".dll");
}

static Dictionary<string, HashSet<string>> LoadTestCoverage(string assemblyPath)
{
    var coverage = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    if (!File.Exists(assemblyPath))
    {
        return coverage;
    }

    var assembly = Assembly.LoadFrom(assemblyPath);
    foreach (var type in assembly.GetTypes())
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            foreach (var attribute in CustomAttributeData.GetCustomAttributes(method)
                         .Where(attribute => attribute.AttributeType == typeof(BehavioralCoverageAttribute)))
            {
                var contractId = (string)attribute.ConstructorArguments[0].Value!;
                var methodKey = $"{type.FullName}.{method.Name}";
                if (!coverage.TryGetValue(methodKey, out var contracts))
                {
                    contracts = new HashSet<string>(StringComparer.Ordinal);
                    coverage.Add(methodKey, contracts);
                }

                contracts.Add(contractId);
            }
        }
    }

    return coverage;
}

static string ExtractMethodKey(string testName)
{
    var parameterList = testName.IndexOf('(');
    return parameterList < 0 ? testName : testName[..parameterList];
}

static async Task WriteReportsAsync(string directory, IProviderAdapter adapter, string project, IReadOnlyList<ConformanceResult> results)
{
    var report = new ConformanceRunReport(
        DateTimeOffset.UtcNow,
        "10.0.14",
        Path.GetFileName(project),
        adapter.Name,
        adapter.Version,
        results.Count,
        results.Count(item => item.Outcome == ConformanceOutcome.Pass),
        results.Count(item => item.Outcome == ConformanceOutcome.Fail),
        results.Count(item => item.Outcome == ConformanceOutcome.Untranslatable),
        results.Count(item => item.Outcome == ConformanceOutcome.Unsupported),
        results.Count(item => item.Outcome == ConformanceOutcome.UndeclaredCapability),
        results.Count(item => item.Outcome == ConformanceOutcome.Error),
        results.Count(item => item.Outcome == ConformanceOutcome.Skip),
        results.Count(item => item.Outcome == ConformanceOutcome.Inconclusive),
        results);
    await using (var stream = File.Create(Path.Combine(directory, "conformance-results.json")))
    {
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter());
        await JsonSerializer.SerializeAsync(stream, report, jsonOptions);
    }

    var cases = results.Select(item =>
    {
        var testCase = new XElement("testcase",
            new XAttribute("classname", item.DisplayName.Contains('.') ? item.DisplayName[..item.DisplayName.LastIndexOf('.')] : "conformance"),
            new XAttribute("name", item.DisplayName));
        if (item.Outcome is ConformanceOutcome.Fail or ConformanceOutcome.Untranslatable or ConformanceOutcome.Error or ConformanceOutcome.UndeclaredCapability)
        {
            testCase.Add(new XElement(item.Outcome is ConformanceOutcome.Error or ConformanceOutcome.UndeclaredCapability ? "error" : "failure",
                new XAttribute("message", item.Reason ?? item.Outcome.ToString()), item.Reason));
        }
        else if (item.Outcome is ConformanceOutcome.Skip or ConformanceOutcome.Unsupported or ConformanceOutcome.Inconclusive)
        {
            testCase.Add(new XElement("skipped", new XAttribute("message", item.Reason ?? item.Outcome.ToString())));
        }

        return testCase;
    });
    var junit = new XDocument(new XElement("testsuites", new XElement("testsuite",
        new XAttribute("name", $"EF Core 10: {adapter.Name}"),
        new XAttribute("tests", results.Count),
        new XAttribute("failures", results.Count(item => item.Outcome is ConformanceOutcome.Fail or ConformanceOutcome.Untranslatable)),
        new XAttribute("errors", results.Count(item => item.Outcome is ConformanceOutcome.Error or ConformanceOutcome.UndeclaredCapability)),
        new XAttribute("skipped", results.Count(item => item.Outcome is ConformanceOutcome.Skip or ConformanceOutcome.Unsupported or ConformanceOutcome.Inconclusive)),
        cases)));
    await File.WriteAllTextAsync(Path.Combine(directory, "conformance-results.junit.xml"), junit.ToString());
}

static string Sanitize(string text, string connectionString) => text.Replace(connectionString, "[redacted connection string]", StringComparison.Ordinal);

static void PrintUsage()
{
    Console.WriteLine("Usage: <adapter-assembly> <adapter-type>");
    Console.WriteLine("       --discover-types <adapter-assembly> <adapter-type> [output-json [test-assembly]]");
    Console.WriteLine("       --run <test-project> <adapter-assembly> <adapter-type> <report-directory>");
    Console.WriteLine("            append --cleanup-database to delete a dedicated disposable database after the run");
    Console.WriteLine("       --run-profile <provider-profile.json> <test-project> <report-directory> [--cleanup-database]");
    Console.WriteLine("       --compare <candidate-results.json> <reference-results.json> <comparison.json>");
    Console.WriteLine("       --verify-defect-control <correct-results.json> <defective-results.json> <output.json> <contract-id> [...]");
    Console.WriteLine("Direct adapter runs use PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING; profiles expand ${ENVIRONMENT_VARIABLE} placeholders.");
}

internal sealed record AdapterLoadReport(string Provider, string ProviderVersion, string AdapterType, string[] DeclaredSupportedFeatures, string[] DeclaredUnsupportedFeatures, string Status);
internal sealed record ConformanceRunReport(DateTimeOffset GeneratedAtUtc, string EfCoreVersion, string TestProject, string Provider, string ProviderVersion, int Total, int Passed, int Failed, int Untranslatable, int Unsupported, int UndeclaredCapabilities, int Errors, int Skipped, int Inconclusive, IReadOnlyList<ConformanceResult> Results);
internal sealed record ConformanceComparisonResult(string ContractId, string DisplayName, ConformanceOutcome? CandidateOutcome, ConformanceOutcome? ReferenceOutcome, string Classification, string? CandidateReason, string? ReferenceReason);
internal sealed record ConformanceComparisonReport(DateTimeOffset GeneratedAtUtc, string EfCoreVersion, string CandidateProvider, string ReferenceProvider, int Total, int PassedOnBoth, int CandidateOnlyFailures, int FailuresAlsoOnReference, IReadOnlyList<ConformanceComparisonResult> Results);

internal sealed class ApiTestAssemblyLoadContext(string assemblyPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(assemblyPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
