using System.Text.Json;
using System.Text.Json.Serialization;
using CoreVar.CommandLineInterface.Distribution;

namespace CoreVar.CommandLineInterface.Publishing;

/// <summary>A declarative, vendor-neutral recipe for atomically completing a CLI release.</summary>
public sealed class ReleaseRecipe
{
    public required Uri Endpoint { get; init; }
    public required string Tenant { get; init; }
    public required string Product { get; init; }
    public required string Version { get; init; }
    public string UploadChannel { get; init; } = "candidate";
    public string PromoteChannel { get; init; } = "stable";
    public required string HostVersion { get; init; }
    public required string FrameworkVersion { get; init; }
    public Dictionary<string, string> Artifacts { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public string? BundleManifest { get; set; }
    public string? BundleSnapshot { get; init; }
    public string? BundleRoot { get; init; }
    public List<string> PostInstallArguments { get; init; } = [];
    public string InstallerOutput { get; set; } = "dist/installers";
}

public sealed class CompleteReleaseRequest
{
    public List<string> RequiredRuntimeIdentifiers { get; init; } = [];
    public ReleaseBundleBootstrap? Bundle { get; init; }
    public List<string> PostInstallArguments { get; init; } = [];
    public required string PromoteChannel { get; init; }
    public required string HostVersion { get; init; }
    public required string FrameworkVersion { get; init; }
}

public sealed class ReleaseRecipePublisher(RegistryPublisher? publisher = null)
{
    private readonly RegistryPublisher _publisher = publisher ?? new();

    public async ValueTask PublishAsync(ReleaseRecipe recipe, string? token = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        // Source-generated init-only deserialization can supply null for omitted defaults.
        var uploadChannel = recipe.UploadChannel ?? "candidate";
        var promoteChannel = recipe.PromoteChannel ?? "stable";
        var installerOutput = recipe.InstallerOutput ?? "dist/installers";
        if (recipe.Artifacts is null || recipe.Artifacts.Count == 0) throw new InvalidDataException("A release recipe requires at least one RID artifact.");
        if (uploadChannel.Equals(promoteChannel, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("UploadChannel and PromoteChannel must differ.");
        if (recipe.BundleManifest is not null && (string.IsNullOrWhiteSpace(recipe.BundleSnapshot) || recipe.PostInstallArguments is null || recipe.PostInstallArguments.Count == 0))
            throw new InvalidDataException("A bundled recipe requires BundleSnapshot and post-install setup arguments.");
        foreach (var artifact in recipe.Artifacts) if (!File.Exists(artifact.Value)) throw new FileNotFoundException($"RID artifact '{artifact.Key}' was not found.", artifact.Value);
        if (recipe.BundleManifest is not null && !File.Exists(recipe.BundleManifest)) throw new FileNotFoundException("Bundle manifest was not found.", recipe.BundleManifest);
        foreach (var artifact in recipe.Artifacts)
            await _publisher.PublishCliAsync(recipe.Endpoint, recipe.Tenant, recipe.Product, recipe.Version,
                artifact.Key, artifact.Value, uploadChannel, token, cancellationToken);

        ReleaseBundleBootstrap? bundle = null;
        if (!string.IsNullOrWhiteSpace(recipe.BundleManifest))
        {
            if (string.IsNullOrWhiteSpace(recipe.BundleSnapshot)) throw new InvalidDataException("BundleSnapshot is required with BundleManifest.");
            var uploaded = await _publisher.PublishBundleAsync(recipe.Endpoint, recipe.Tenant, recipe.Product,
                recipe.BundleSnapshot, recipe.BundleManifest, token, cancellationToken);
            bundle = new ReleaseBundleBootstrap { Manifest = uploaded.Manifest, Sha256 = uploaded.Sha256, Snapshot = uploaded.Snapshot, Root = recipe.BundleRoot };
        }
        await _publisher.CompleteReleaseAsync(recipe.Endpoint, recipe.Tenant, recipe.Product, recipe.Version,
            new CompleteReleaseRequest { RequiredRuntimeIdentifiers = [.. recipe.Artifacts.Keys], Bundle = bundle,
                PostInstallArguments = recipe.PostInstallArguments ?? [], PromoteChannel = promoteChannel,
                HostVersion = recipe.HostVersion, FrameworkVersion = recipe.FrameworkVersion }, token, cancellationToken);

        Directory.CreateDirectory(installerOutput);
        var catalog = new Uri(new Uri(recipe.Endpoint.AbsoluteUri.TrimEnd('/') + "/"),
            $"v1/{Uri.EscapeDataString(recipe.Tenant)}/products/{Uri.EscapeDataString(recipe.Product)}/catalog.json");
        await File.WriteAllTextAsync(Path.Combine(installerOutput, "install.ps1"), InstallerScriptGenerator.PowerShell(recipe.Product, catalog, promoteChannel), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(installerOutput, "install.sh"), InstallerScriptGenerator.Shell(recipe.Product, catalog, promoteChannel), cancellationToken);
    }

    public static ReleaseRecipe Load(string path)
    {
        var recipe = JsonSerializer.Deserialize(File.ReadAllText(path), PublishingJsonContext.Default.ReleaseRecipe) ?? throw new InvalidDataException("Release recipe is empty.");
        var root = Path.GetDirectoryName(Path.GetFullPath(path))!;
        if (recipe.Artifacts is null) throw new InvalidDataException("A release recipe requires RID artifacts.");
        foreach (var key in recipe.Artifacts.Keys.ToArray()) if (!Path.IsPathRooted(recipe.Artifacts[key])) recipe.Artifacts[key] = Path.GetFullPath(Path.Combine(root, recipe.Artifacts[key]));
        if (recipe.BundleManifest is not null && !Path.IsPathRooted(recipe.BundleManifest)) recipe.BundleManifest = Path.GetFullPath(Path.Combine(root, recipe.BundleManifest));
        recipe.InstallerOutput ??= "dist/installers";
        if (!Path.IsPathRooted(recipe.InstallerOutput)) recipe.InstallerOutput = Path.GetFullPath(Path.Combine(root, recipe.InstallerOutput));
        return recipe;
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(ReleaseRecipe))]
[JsonSerializable(typeof(CompleteReleaseRequest))]
public partial class PublishingJsonContext : JsonSerializerContext;
