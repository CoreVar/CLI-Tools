using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreVar.CommandLineInterface.Distribution;

namespace CoreVar.CommandLineInterface.Publishing;

public static class InstallerScriptGenerator
{
    public static string PowerShell(string product, Uri catalog, string channel = "stable",
        IReadOnlyDictionary<string, string>? trustedPublicKeys = null, bool requireSignature = false) =>
        Resource("install.ps1").Replace("__CONFIG__", Config(product, catalog, channel, trustedPublicKeys, requireSignature));

    public static string Shell(string product, Uri catalog, string channel = "stable",
        IReadOnlyDictionary<string, string>? trustedPublicKeys = null, bool requireSignature = false) =>
        "#!/usr/bin/env sh\nset -eu\ncommand -v python3 >/dev/null || { echo 'Python 3 is required' >&2; exit 69; }\n" +
        "exec python3 - \"$@\" <<'COREVAR_INSTALLER_PYTHON'\n" +
        Resource("install.py").Replace("__CONFIG__", Config(product, catalog, channel, trustedPublicKeys, requireSignature)) +
        "\nCOREVAR_INSTALLER_PYTHON\n";

    private static string Config(string product, Uri catalog, string channel, IReadOnlyDictionary<string, string>? keys, bool required)
    {
        _ = new DistributionPaths(Path.GetTempPath()).Version(product);
        _ = new DistributionPaths(Path.GetTempPath()).Version(channel);
        if (product.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.'))
            throw new ArgumentException("Product must contain only ASCII letters, digits, dots, hyphens or underscores.", nameof(product));
        if (!catalog.IsAbsoluteUri || (catalog.Scheme != "https" && catalog.Scheme != "http" && !catalog.IsFile))
            throw new ArgumentException("Catalog must be an absolute HTTP(S) or file URI.", nameof(catalog));
        if (required && (keys is null || keys.Count == 0)) throw new ArgumentException("Signature enforcement requires trusted public keys.");
        var config = new InstallerConfiguration { Product = product, Catalog = catalog.AbsoluteUri, Channel = channel,
            TrustedPublicKeys = keys?.ToDictionary(p => p.Key, p => p.Value) ?? [], RequireSignature = required };
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(config, InstallerJsonContext.Default.InstallerConfiguration)));
    }

    private static string Resource(string name)
    {
        using var stream = typeof(InstallerScriptGenerator).Assembly.GetManifestResourceStream("CoreVar.Installers." + name)
            ?? throw new InvalidOperationException($"Installer resource '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}

internal sealed class InstallerConfiguration
{
    public required string Product { get; init; }
    public required string Catalog { get; init; }
    public required string Channel { get; init; }
    public Dictionary<string, string> TrustedPublicKeys { get; init; } = [];
    public bool RequireSignature { get; init; }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(InstallerConfiguration))]
internal partial class InstallerJsonContext : JsonSerializerContext;
