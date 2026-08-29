namespace CoreVar.CommandLineInterface.Registry;

public sealed class RegistryOptions
{
    public string DataRoot { get; init; } = Environment.GetEnvironmentVariable("COREVAR_REGISTRY_DATA") ?? "/data";
    public bool AllowAnonymousPublish { get; init; } = bool.TryParse(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_ALLOW_ANONYMOUS_PUBLISH"), out var enabled) && enabled;
    public IReadOnlyDictionary<string, string> TenantKeys { get; init; } = ParseKeys(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_API_KEYS"));

    private static IReadOnlyDictionary<string, string> ParseKeys(string? value)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(value)) return result;
        foreach (var item in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = item.IndexOf('=');
            if (separator > 0) result[item[..separator]] = item[(separator + 1)..];
        }
        return result;
    }
}
