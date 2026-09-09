namespace CoreVar.CommandLineInterface.Registry;

public sealed class RegistryOptions
{
    public const long DefaultMaxUploadBytes = 128L * 1024 * 1024;
    public string DataRoot { get; init; } = Environment.GetEnvironmentVariable("COREVAR_REGISTRY_DATA") ?? "/data";
    public string PathBase { get; init; } = NormalizePathBase(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_PATH_BASE"));
    public Uri? PublicBaseUri { get; init; } = ParsePublicBaseUri(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_PUBLIC_BASE_URL"));
    public bool TrustForwardedHeaders { get; init; } = bool.TryParse(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_TRUST_FORWARDED_HEADERS"), out var trustForwardedHeaders) && trustForwardedHeaders;
    public string? OidcAuthority { get; init; } = Environment.GetEnvironmentVariable("COREVAR_REGISTRY_OIDC_AUTHORITY");
    public string OidcAudience { get; init; } = Environment.GetEnvironmentVariable("COREVAR_REGISTRY_OIDC_AUDIENCE") ?? "corevar-cli-registry";
    public bool OidcRequireHttpsMetadata { get; init; } = !bool.TryParse(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_OIDC_REQUIRE_HTTPS_METADATA"), out var requireHttps) || requireHttps;
    public bool RequireAuthenticatedReads { get; init; } = bool.TryParse(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_REQUIRE_AUTHENTICATED_READS"), out var authenticatedReads) && authenticatedReads;
    public bool AllowAnonymousPublish { get; init; } = bool.TryParse(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_ALLOW_ANONYMOUS_PUBLISH"), out var enabled) && enabled;
    public IReadOnlyDictionary<string, string> TenantKeys { get; init; } = ParseKeys(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_API_KEYS"));
    public long MaxUploadBytes { get; init; } = ParseMaxUploadBytes(Environment.GetEnvironmentVariable("COREVAR_REGISTRY_MAX_UPLOAD_BYTES"));
    // JSON shape: { "tenant/product": { "key-id": "-----BEGIN PUBLIC KEY-----..." } }
    public Dictionary<string, Dictionary<string, string>> TrustedSigningKeys { get; init; } = LoadSigningKeys();
    public string[] SignedChannels { get; init; } = (Environment.GetEnvironmentVariable("COREVAR_REGISTRY_SIGNED_CHANNELS") ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static Dictionary<string, Dictionary<string, string>> LoadSigningKeys()
    {
        var path = Environment.GetEnvironmentVariable("COREVAR_REGISTRY_SIGNING_KEYS_FILE");
        return string.IsNullOrWhiteSpace(path) ? [] : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Signing keys file is empty.");
    }

    internal static long ParseMaxUploadBytes(string? value) =>
        long.TryParse(value, out var parsed) && parsed > 0 ? parsed : DefaultMaxUploadBytes;

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

public sealed class RegistryUploadTooLargeException(long maximum) : Exception($"Upload exceeds the configured {maximum}-byte limit.");
