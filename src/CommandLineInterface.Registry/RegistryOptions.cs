namespace CoreVar.CommandLineInterface.Registry;

public sealed class RegistryOptions
{
    public string DataRoot { get; init; } = Environment.GetEnvironmentVariable("COREVAR_REGISTRY_DATA") ?? "/data";
    public string PathBase { get; init; } = NormalizePathBase(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_PATH_BASE"));
    public Uri? PublicBaseUri { get; init; } = ParsePublicBaseUri(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_PUBLIC_BASE_URL"));
    public bool TrustForwardedHeaders { get; init; } = bool.TryParse(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_TRUST_FORWARDED_HEADERS"), out var trustForwardedHeaders) && trustForwardedHeaders;
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

    private static string NormalizePathBase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "/") return string.Empty;
        return "/" + value.Trim().Trim('/');
    }

    private static Uri? ParsePublicBaseUri(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("COREVAR_REGISTRY_PUBLIC_BASE_URL must be an absolute HTTP or HTTPS URL.");
        return new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
    }
}
