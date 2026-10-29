using System.Text.Json;
using System.Text.RegularExpressions;

namespace pengdows.efcore.conformance;

/// <summary>Environment-specific adapter selection and connection configuration for one provider run.</summary>
public sealed record ProviderRunConfiguration(
    string Name,
    string AdapterAssembly,
    string AdapterType,
    string ConnectionString)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static ProviderRunConfiguration Load(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var configuration = JsonSerializer.Deserialize<ProviderRunConfiguration>(File.ReadAllText(fullPath), JsonOptions)
            ?? throw new InvalidOperationException($"Provider configuration '{path}' is empty.");
        if (string.IsNullOrWhiteSpace(configuration.Name) ||
            string.IsNullOrWhiteSpace(configuration.AdapterAssembly) ||
            string.IsNullOrWhiteSpace(configuration.AdapterType) ||
            string.IsNullOrWhiteSpace(configuration.ConnectionString))
        {
            throw new InvalidOperationException($"Provider configuration '{path}' needs name, adapterAssembly, adapterType, and connectionString.");
        }

        return configuration with
        {
            AdapterAssembly = Path.GetFullPath(Path.IsPathRooted(configuration.AdapterAssembly)
                ? configuration.AdapterAssembly
                : Path.Combine(Path.GetDirectoryName(fullPath)!, configuration.AdapterAssembly))
        };
    }

    public string ResolveConnectionString() => Regex.Replace(
        ConnectionString,
        @"\$\{([A-Za-z_][A-Za-z0-9_]*)\}",
        match => Environment.GetEnvironmentVariable(match.Groups[1].Value) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Provider configuration '{Name}' requires environment variable {match.Groups[1].Value}."));
}
