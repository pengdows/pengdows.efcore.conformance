using System.Diagnostics;
using System.Reflection;
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
    var typeReport = AdoNetTypeDiscovery.Discover(connection, adapter as ISupplementalTypeDiscovery);
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
            nativeDiscoveryReport = AdoNetTypeDiscovery.Discover(discoveryConnection, runAdapter as ISupplementalTypeDiscovery);
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
                        if (!clrType.IsArray && clrType != typeof(byte[]) &&
                            !mapping.DatabaseTypeName.Contains("json", StringComparison.OrdinalIgnoreCase) &&
                            !mapping.DatabaseTypeName.Equals("xml", StringComparison.OrdinalIgnoreCase) &&
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
    var options = new DbContextOptionsBuilder();
    adapter.Configure(options, connectionString);
    await using var context = new DbContext(options.Options);
    await adapter.CleanupDatabaseAsync(context, CancellationToken.None);
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
    Console.WriteLine("Direct adapter runs use PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING; profiles expand ${ENVIRONMENT_VARIABLE} placeholders.");
}

internal sealed record AdapterLoadReport(string Provider, string ProviderVersion, string AdapterType, string[] DeclaredSupportedFeatures, string[] DeclaredUnsupportedFeatures, string Status);
internal sealed record ConformanceRunReport(DateTimeOffset GeneratedAtUtc, string EfCoreVersion, string TestProject, string Provider, string ProviderVersion, int Total, int Passed, int Failed, int Untranslatable, int Unsupported, int UndeclaredCapabilities, int Errors, int Skipped, int Inconclusive, IReadOnlyList<ConformanceResult> Results);
internal sealed record ConformanceComparisonResult(string ContractId, string DisplayName, ConformanceOutcome? CandidateOutcome, ConformanceOutcome? ReferenceOutcome, string Classification, string? CandidateReason, string? ReferenceReason);
internal sealed record ConformanceComparisonReport(DateTimeOffset GeneratedAtUtc, string EfCoreVersion, string CandidateProvider, string ReferenceProvider, int Total, int PassedOnBoth, int CandidateOnlyFailures, int FailuresAlsoOnReference, IReadOnlyList<ConformanceComparisonResult> Results);
