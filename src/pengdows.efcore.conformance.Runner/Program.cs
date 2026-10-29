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

if (args[0] == "--run")
{
    return await RunTestsAsync(args);
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
            NativeTypeCoverage = BuildNativeTypeCoverage(typeReport, Path.GetFullPath(args[4]))
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

static IReadOnlyList<NativeTypeCoverageResult> BuildNativeTypeCoverage(TypeDiscoveryReport report, string testAssemblyPath)
{
    var assembly = Assembly.LoadFrom(testAssemblyPath);
    var cases = new List<(string DatabaseTypeName, string ClrTypeName, string ContractId)>();
    foreach (var type in assembly.GetTypes())
    {
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            foreach (var attribute in CustomAttributeData.GetCustomAttributes(method)
                         .Where(attribute => attribute.AttributeType.FullName == typeof(NativeTypeCoverageAttribute).FullName))
            {
                var args = attribute.ConstructorArguments;
                if (args.Count == 3 && args[0].Value is string databaseTypeName &&
                    args[1].Value is Type clrType && args[2].Value is string contractId)
                {
                    cases.Add((databaseTypeName, clrType.FullName ?? clrType.Name, contractId));
                }
            }
        }
    }

    var observedTypes = report.Types.Concat(report.SupplementalTypes)
        .Select(type => type.DatabaseTypeName.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var results = new List<NativeTypeCoverageResult>();
    foreach (var observedType in observedTypes)
    {
        var matches = cases.Where(testCase => string.Equals(
                testCase.DatabaseTypeName.Trim(), observedType, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var familyMatches = matches.Length > 0
            ? matches
            : cases.Where(testCase => string.Equals(
                    GetNativeTypeFamily(testCase.DatabaseTypeName),
                    GetNativeTypeFamily(observedType),
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
        results.Add(new NativeTypeCoverageResult(
            observedType,
            matches.Length > 0 ? "COVERED" : familyMatches.Length > 0 ? "PARTIAL" : "UNCOVERED",
            familyMatches.Select(item => item.ClrTypeName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            familyMatches.Select(item => item.ContractId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
    }

    var unmatchedCases = cases.Where(testCase => !observedTypes.Contains(testCase.DatabaseTypeName.Trim(), StringComparer.OrdinalIgnoreCase));
    results.AddRange(unmatchedCases
        .GroupBy(testCase => testCase.DatabaseTypeName.Trim(), StringComparer.OrdinalIgnoreCase)
        .Select(group => new NativeTypeCoverageResult(
            group.Key,
            "NOT_OBSERVED",
            group.Select(item => item.ClrTypeName).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            group.Select(item => item.ContractId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray())));
    return results.OrderBy(item => item.DatabaseTypeName, StringComparer.OrdinalIgnoreCase).ToArray();
}

static string GetNativeTypeFamily(string databaseTypeName)
{
    var trimmed = databaseTypeName.Trim();
    var facetsStart = trimmed.IndexOf('(');
    return facetsStart < 0 ? trimmed : trimmed[..facetsStart].TrimEnd();
}

async Task<int> RunTestsAsync(string[] arguments)
{
    if (arguments.Length != 5)
    {
        Console.Error.WriteLine("Usage: --run <test-project> <adapter-assembly> <adapter-type> <report-directory>");
        return 2;
    }

    var cs = GetConnectionString();
    var project = Path.GetFullPath(arguments[1]);
    var adapterPath = Path.GetFullPath(arguments[2]);
    var adapterType = arguments[3];
    var reportDirectory = Path.GetFullPath(arguments[4]);
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
    var coverageByMethod = LoadTestCoverage(GetTestAssemblyPath(project));
    var results = File.Exists(trxPath) ? ReadResults(trxPath, cs, coverageByMethod) : [];
    if (File.Exists(trxPath))
    {
        var trxText = await File.ReadAllTextAsync(trxPath);
        await File.WriteAllTextAsync(trxPath, Sanitize(trxText, cs));
    }
    if (results.Count == 0)
    {
        results.Add(new ConformanceResult("RUNNER-NO-RESULTS", "Test host produced no test results", ConformanceOutcome.Error, $"See {Path.GetFileName(logPath)} for the test host output."));
    }

    await WriteReportsAsync(reportDirectory, runAdapter, project, results);
    Console.WriteLine($"Provider: {runAdapter.Name} {runAdapter.Version}");
    Console.WriteLine($"Results: {results.Count} total; {results.Count(item => item.Outcome == ConformanceOutcome.Pass)} passed; {results.Count(item => item.Outcome == ConformanceOutcome.Fail)} failed; {results.Count(item => item.Outcome == ConformanceOutcome.Unsupported)} unsupported; {results.Count(item => item.Outcome == ConformanceOutcome.Error)} errors; {results.Count(item => item.Outcome == ConformanceOutcome.Skip)} skipped.");
    Console.WriteLine($"Reports written to {reportDirectory}");
    return process.ExitCode;
}

static string GetConnectionString() =>
    Environment.GetEnvironmentVariable("PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING") is { Length: > 0 } value
        ? value
        : throw new InvalidOperationException("PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING is required.");

static IProviderAdapter LoadAdapter(string assemblyPath, string typeName)
    => ProviderAdapterLoader.Load(assemblyPath, typeName);

static List<ConformanceResult> ReadResults(string trxPath, string connectionString, IReadOnlyDictionary<string, HashSet<string>> coverageByMethod)
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
        var outcome = outcomeText.ToUpperInvariant() switch
        {
            "PASSED" => ConformanceOutcome.Pass,
            "NOTEXECUTED" when message?.StartsWith("UNSUPPORTED:", StringComparison.OrdinalIgnoreCase) == true => ConformanceOutcome.Unsupported,
            "NOTEXECUTED" or "INCONCLUSIVE" => ConformanceOutcome.Skip,
            _ when exceptionType?.EndsWith(nameof(ConformanceInfrastructureException), StringComparison.Ordinal) == true => ConformanceOutcome.Error,
            _ => ConformanceOutcome.Fail
        };
        var methodKey = ExtractMethodKey(name);
        var contractId = coverageByMethod.TryGetValue(methodKey, out var contracts)
            ? string.Join(",", contracts.Order(StringComparer.Ordinal))
            : name;
        return new ConformanceResult(contractId, name, outcome, message is null ? null : Sanitize(message, connectionString), exceptionType);
    }).ToList();
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
        results.Count(item => item.Outcome == ConformanceOutcome.Unsupported),
        results.Count(item => item.Outcome == ConformanceOutcome.Error),
        results.Count(item => item.Outcome == ConformanceOutcome.Skip),
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
        if (item.Outcome is ConformanceOutcome.Fail or ConformanceOutcome.Error)
        {
            testCase.Add(new XElement(item.Outcome == ConformanceOutcome.Error ? "error" : "failure",
                new XAttribute("message", item.Reason ?? item.Outcome.ToString()), item.Reason));
        }
        else if (item.Outcome is ConformanceOutcome.Skip or ConformanceOutcome.Unsupported)
        {
            testCase.Add(new XElement("skipped", new XAttribute("message", item.Reason ?? item.Outcome.ToString())));
        }

        return testCase;
    });
    var junit = new XDocument(new XElement("testsuites", new XElement("testsuite",
        new XAttribute("name", $"EF Core 10: {adapter.Name}"),
        new XAttribute("tests", results.Count),
        new XAttribute("failures", results.Count(item => item.Outcome == ConformanceOutcome.Fail)),
        new XAttribute("errors", results.Count(item => item.Outcome == ConformanceOutcome.Error)),
        new XAttribute("skipped", results.Count(item => item.Outcome is ConformanceOutcome.Skip or ConformanceOutcome.Unsupported)),
        cases)));
    await File.WriteAllTextAsync(Path.Combine(directory, "conformance-results.junit.xml"), junit.ToString());
}

static string Sanitize(string text, string connectionString) => text.Replace(connectionString, "[redacted connection string]", StringComparison.Ordinal);

static void PrintUsage()
{
    Console.WriteLine("Usage: <adapter-assembly> <adapter-type>");
    Console.WriteLine("       --discover-types <adapter-assembly> <adapter-type> [output-json [test-assembly]]");
    Console.WriteLine("       --run <test-project> <adapter-assembly> <adapter-type> <report-directory>");
    Console.WriteLine("Set PENGDOWS_EFCORE_CONFORMANCE_CONNECTION_STRING in the environment.");
}

internal sealed record AdapterLoadReport(string Provider, string ProviderVersion, string AdapterType, string[] DeclaredSupportedFeatures, string[] DeclaredUnsupportedFeatures, string Status);
internal sealed record ConformanceRunReport(DateTimeOffset GeneratedAtUtc, string EfCoreVersion, string TestProject, string Provider, string ProviderVersion, int Total, int Passed, int Failed, int Unsupported, int Errors, int Skipped, IReadOnlyList<ConformanceResult> Results);
