using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using CoreVar.CommandLineInterface.Distribution;
using CoreVar.CommandLineInterface.Modules;

namespace CoreVar.CommandLineInterface.Registry;

public sealed class FileRegistryStore(RegistryOptions options)
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public async ValueTask<ReleaseCatalog?> GetReleaseCatalogAsync(string tenant, string product, CancellationToken cancellationToken)
    {
        var path = ReleaseCatalogPath(tenant, product);
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync(stream, DistributionJsonContext.Default.ReleaseCatalog, cancellationToken);
    }

    public async ValueTask<ModuleCatalog?> GetModuleCatalogAsync(string tenant, CancellationToken cancellationToken)
    {
        var path = ModuleCatalogPath(tenant);
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<ModuleCatalog>(stream, cancellationToken: cancellationToken);
    }

    public async ValueTask<ReleaseArtifact> PublishReleaseAsync(string tenant, string product, string version,
        string rid, string channel, Stream content, Uri publicUri, CancellationToken cancellationToken)
    {
        Validate(tenant); Validate(product); Validate(version); Validate(rid); Validate(channel);
        var gate = _locks.GetOrAdd($"release:{tenant}:{product}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var blobPath = BlobPath(tenant, "products", product, version, rid + ".zip");
            var (digest, size) = await WriteBlobAsync(blobPath, content, cancellationToken);
            var artifact = new ReleaseArtifact { RuntimeIdentifier = rid, Uri = publicUri, Sha256 = digest, Size = size };
            var catalog = await GetReleaseCatalogAsync(tenant, product, cancellationToken) ?? new ReleaseCatalog { Product = product };
            var release = catalog.Releases.FirstOrDefault(item => item.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
            if (release is null) { release = new ReleaseManifest { Version = version, PublishedAt = DateTimeOffset.UtcNow }; catalog.Releases.Add(release); }
            release.Artifacts.RemoveAll(item => item.RuntimeIdentifier.Equals(rid, StringComparison.OrdinalIgnoreCase));
            release.Artifacts.Add(artifact);
            catalog.Channels[channel] = version;
            await WriteJsonAtomicAsync(ReleaseCatalogPath(tenant, product), catalog, DistributionJsonContext.Default.ReleaseCatalog, cancellationToken);
            return artifact;
        }
        finally { gate.Release(); }
    }

    public async ValueTask<ModuleRelease> PublishModuleAsync(string tenant, string id, string version, string channel,
        string description, Stream content, Uri publicUri, CancellationToken cancellationToken)
    {
        Validate(tenant); Validate(id); Validate(version); Validate(channel);
        var gate = _locks.GetOrAdd($"module:{tenant}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var blobPath = BlobPath(tenant, "modules", id, version, "module.zip");
            var (digest, _) = await WriteBlobAsync(blobPath, content, cancellationToken);
            var release = new ModuleRelease { Version = version, Channel = channel, Package = publicUri, Sha256 = digest, PublishedAt = DateTimeOffset.UtcNow };
            var catalog = await GetModuleCatalogAsync(tenant, cancellationToken) ?? new ModuleCatalog();
            var module = catalog.Modules.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (module is null) { module = new ModuleCatalogEntry { Id = id, Description = description }; catalog.Modules.Add(module); }
            module.Releases.RemoveAll(item => item.Version.Equals(version, StringComparison.OrdinalIgnoreCase) && item.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));
            module.Releases.Add(release);
            await WriteJsonAtomicAsync(ModuleCatalogPath(tenant), catalog, null, cancellationToken);
            return release;
        }
        finally { gate.Release(); }
    }

    public string GetBlobPath(string tenant, params string[] segments) => BlobPath(tenant, segments);

    public async ValueTask PromoteAsync(string tenant, string product, string channel, string version,
        int percentage, string? fallbackVersion, string seed, CancellationToken cancellationToken)
    {
        Validate(tenant); Validate(product); Validate(channel); Validate(version);
        var gate = _locks.GetOrAdd($"release:{tenant}:{product}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var catalog = await GetReleaseCatalogAsync(tenant, product, cancellationToken) ?? throw new KeyNotFoundException("Product catalog not found.");
            if (!catalog.Releases.Any(item => item.Version.Equals(version, StringComparison.OrdinalIgnoreCase))) throw new KeyNotFoundException("Release not found.");
            catalog.Channels[channel] = version;
            catalog.Rollouts.RemoveAll(item => item.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));
            if (percentage < 100) catalog.Rollouts.Add(new ChannelRollout { Channel = channel, Version = version, FallbackVersion = fallbackVersion, Percentage = Math.Clamp(percentage, 0, 100), Seed = seed });
            await WriteJsonAtomicAsync(ReleaseCatalogPath(tenant, product), catalog, DistributionJsonContext.Default.ReleaseCatalog, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async ValueTask RevokeAsync(string tenant, string product, string? version, string? sha256,
        string reason, CancellationToken cancellationToken)
    {
        if (version is null && sha256 is null) throw new ArgumentException("A version or SHA-256 digest is required.");
        var gate = _locks.GetOrAdd($"release:{tenant}:{product}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var catalog = await GetReleaseCatalogAsync(tenant, product, cancellationToken) ?? throw new KeyNotFoundException("Product catalog not found.");
            catalog.Revocations.Add(new Revocation { Version = version, Sha256 = sha256, Reason = reason, RevokedAt = DateTimeOffset.UtcNow });
            await WriteJsonAtomicAsync(ReleaseCatalogPath(tenant, product), catalog, DistributionJsonContext.Default.ReleaseCatalog, cancellationToken);
        }
        finally { gate.Release(); }
    }

    private async ValueTask<(string Digest, long Size)> WriteBlobAsync(string path, Stream content, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".upload-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var output = File.Create(temporary)) await content.CopyToAsync(output, cancellationToken);
            string digest;
            long size;
            await using (var verification = File.OpenRead(temporary))
            {
                digest = Convert.ToHexString(await SHA256.HashDataAsync(verification, cancellationToken));
                size = verification.Length;
            }
            File.Move(temporary, path, true);
            return (digest, size);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static async ValueTask WriteJsonAtomicAsync<T>(string path, T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>? typeInfo, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".new-" + Guid.NewGuid().ToString("N");
        await using (var stream = File.Create(temporary))
        {
            if (typeInfo is null) await JsonSerializer.SerializeAsync(stream, value, cancellationToken: cancellationToken);
            else await JsonSerializer.SerializeAsync(stream, value, typeInfo, cancellationToken);
        }
        File.Move(temporary, path, true);
    }

    private string ReleaseCatalogPath(string tenant, string product) => Path.Combine(Tenant(tenant), "products", Safe(product), "catalog.json");
    private string ModuleCatalogPath(string tenant) => Path.Combine(Tenant(tenant), "modules", "catalog.json");
    private string BlobPath(string tenant, params string[] segments) => Path.Combine([Tenant(tenant), "blobs", .. segments.Select(Safe)]);
    private string Tenant(string tenant) => Path.Combine(Path.GetFullPath(options.DataRoot), "tenants", Safe(tenant));
    private static void Validate(string value) => _ = Safe(value);
    private static string Safe(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || value.Contains('/') || value.Contains('\\') || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("Registry identifiers must be safe path segments.");
        return value;
    }
}
