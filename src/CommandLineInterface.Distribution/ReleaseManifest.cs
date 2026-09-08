using System.Text.Json.Serialization;

namespace CoreVar.CommandLineInterface.Distribution;

public sealed class ReleaseCatalog
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string Product { get; init; }
    public List<ReleaseManifest> Releases { get; init; } = [];
    public Dictionary<string, string> Channels { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public List<ChannelRollout> Rollouts { get; init; } = [];
    public List<Revocation> Revocations { get; init; } = [];
}

public sealed class ChannelRollout
{
    public required string Channel { get; init; }
    public required string Version { get; init; }
    public string? FallbackVersion { get; init; }
    public int Percentage { get; init; } = 100;
    public string Seed { get; init; } = "default";
}

public sealed class ReleaseManifest
{
    public required string Version { get; init; }
    public DateTimeOffset PublishedAt { get; init; }
    public string? MinimumLauncherVersion { get; init; }
    public List<ReleaseArtifact> Artifacts { get; init; } = [];
    public Dictionary<string, string> Metadata { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public ReleaseBundleBootstrap? Bundle { get; init; }
    public List<string> PostInstallArguments { get; init; } = [];
}

public sealed class ReleaseBundleBootstrap
{
    public required Uri Manifest { get; init; }
    public required string Sha256 { get; init; }
    public required string Snapshot { get; init; }
    public string? Root { get; init; }
}

public sealed class ReleaseArtifact
{
    public required string RuntimeIdentifier { get; init; }
    public required Uri Uri { get; init; }
    public required string Sha256 { get; init; }
    public long Size { get; init; }
    public string Format { get; init; } = "zip";
    public string? Signature { get; init; }
    public string? SigningKeyId { get; init; }
}

public sealed class Revocation
{
    public string? Version { get; init; }
    public string? Sha256 { get; init; }
    public required string Reason { get; init; }
    public DateTimeOffset RevokedAt { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter<InstallationProvider>))]
public enum InstallationProvider { Direct, DotNetTool, WinGet, Homebrew, Apt, Rpm, Msix, Msi, Unknown }

public sealed class InstallationState
{
    public string SchemaVersion { get; init; } = "1.0";
    public required string Product { get; init; }
    public required string Version { get; init; }
    public string? PreviousVersion { get; init; }
    public string Channel { get; init; } = "stable";
    public required Uri Catalog { get; init; }
    public InstallationProvider Provider { get; init; } = InstallationProvider.Direct;
    public string? PackageId { get; init; }
    public string? Entrypoint { get; init; }
    public string? LauncherVersion { get; init; }
    public string InstallationId { get; init; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset ActivatedAt { get; init; } = DateTimeOffset.UtcNow;
}
