using System.Security.Cryptography;
using System.Text.Json;
using CoreVar.CommandLineInterface.Distribution;
using CoreVar.CommandLineInterface.Modules;

namespace CoreVar.CommandLineInterface.Publishing;

/// <summary>Publishes a registry as static files suitable for GitHub Pages or any HTTP host.</summary>
public sealed class StaticRegistryPublisher(string root, Uri publicBaseUri)
{
    private readonly string _root = Path.GetFullPath(root);
    private readonly Uri _publicBase = new(publicBaseUri.AbsoluteUri.TrimEnd('/') + "/");

    public async ValueTask<ReleaseArtifact> PublishCliAsync(string tenant, string product, string version, string rid,
        string artifactPath, string channel = "stable", CancellationToken cancellationToken = default)
    {
        Validate(tenant); Validate(product); Validate(version); Validate(rid); Validate(channel);
        var relative = Path.Combine("v1", tenant, "blobs", "products", product, version, rid + Path.GetExtension(artifactPath)).Replace('\\', '/');
        var target = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        var (digest, size) = await CopyAsync(artifactPath, target, cancellationToken);
        var artifact = new ReleaseArtifact { RuntimeIdentifier = rid, Uri = new Uri(_publicBase, relative), Sha256 = digest, Size = size, Format = Path.GetExtension(artifactPath).TrimStart('.') };
        var catalogPath = Path.Combine(_root, "v1", tenant, "products", product, "catalog.json");
        var catalog = await ReadAsync(catalogPath, DistributionJsonContext.Default.ReleaseCatalog, cancellationToken) ?? new ReleaseCatalog { Product = product };
        var release = catalog.Releases.FirstOrDefault(item => item.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
        if (release is null) { release = new ReleaseManifest { Version = version, PublishedAt = DateTimeOffset.UtcNow }; catalog.Releases.Add(release); }
        release.Artifacts.RemoveAll(item => item.RuntimeIdentifier.Equals(rid, StringComparison.OrdinalIgnoreCase));
        release.Artifacts.Add(artifact); catalog.Channels[channel] = version;
        await WriteAsync(catalogPath, catalog, DistributionJsonContext.Default.ReleaseCatalog, cancellationToken);
        return artifact;
    }

    public async ValueTask<ModuleRelease> PublishModuleAsync(string tenant, string id, string version, string packagePath,
        string channel = "stable", string? description = null, CancellationToken cancellationToken = default)
    {
        Validate(tenant); Validate(id); Validate(version); Validate(channel);
        var relative = Path.Combine("v1", tenant, "blobs", "modules", id, version, "module.zip").Replace('\\', '/');
        var target = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        var (digest, _) = await CopyAsync(packagePath, target, cancellationToken);
        var release = new ModuleRelease { Version = version, Channel = channel, Package = new Uri(_publicBase, relative), Sha256 = digest, PublishedAt = DateTimeOffset.UtcNow };
        var catalogPath = Path.Combine(_root, "v1", tenant, "modules", "catalog.json");
        var catalog = await ReadAsync(catalogPath, ModuleJsonContext.Default.ModuleCatalog, cancellationToken) ?? new ModuleCatalog();
        var module = catalog.Modules.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (module is null) { module = new ModuleCatalogEntry { Id = id, Description = description }; catalog.Modules.Add(module); }
        module.Releases.RemoveAll(item => item.Version.Equals(version, StringComparison.OrdinalIgnoreCase) && item.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));
        module.Releases.Add(release);
        await WriteAsync(catalogPath, catalog, ModuleJsonContext.Default.ModuleCatalog, cancellationToken);
        return release;
    }

    private static async ValueTask<(string Digest, long Size)> CopyAsync(string source, string target, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using (var input = File.OpenRead(source)) await using (var output = File.Create(target)) await input.CopyToAsync(output, cancellationToken);
        await using var verify = File.OpenRead(target);
        return (Convert.ToHexString(await SHA256.HashDataAsync(verify, cancellationToken)), verify.Length);
    }

    private static async ValueTask<T?> ReadAsync<T>(string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return default;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync(stream, type, cancellationToken);
    }

    private static async ValueTask WriteAsync<T>(string path, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, type, cancellationToken);
    }

    private static void Validate(string value) => _ = new DistributionPaths(Path.GetTempPath()).Version(value);
}
