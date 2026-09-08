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
            var (digest, size) = await WriteBlobAsync(blobPath, content, cancellationToken, immutable: true);
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

    public async ValueTask<BundleSnapshotResult> PublishBundleSnapshotAsync(string tenant, string product, string snapshot,
        Stream content, Uri publicUri, CancellationToken cancellationToken)
    {
        Validate(tenant); Validate(product); Validate(snapshot);
        var gate = _locks.GetOrAdd($"bundle:{tenant}:{product}:{snapshot}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var path = BlobPath(tenant, "products", product, "bundles", snapshot + ".json");
            var (digest, size) = await WriteBlobAsync(path, content, cancellationToken, immutable: true);
            return new BundleSnapshotResult(publicUri, digest, size, snapshot);
        }
        finally { gate.Release(); }
    }

    public async ValueTask SetReleaseMetadataAsync(string tenant, string product, string version,
        ReleaseBundleBootstrap? bundle, IReadOnlyList<string>? postInstallArguments, CancellationToken cancellationToken)
    {
        Validate(tenant); Validate(product); Validate(version);
        var gate = _locks.GetOrAdd($"release:{tenant}:{product}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var catalog = await GetReleaseCatalogAsync(tenant, product, cancellationToken)
                ?? throw new KeyNotFoundException("Product catalog not found.");
            var release = catalog.Releases.FirstOrDefault(item => item.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
                ?? throw new KeyNotFoundException("Release not found.");
            release.Bundle = bundle;
            release.PostInstallArguments.Clear();
            if (postInstallArguments is not null) release.PostInstallArguments.AddRange(postInstallArguments);
            await WriteJsonAtomicAsync(ReleaseCatalogPath(tenant, product), catalog, DistributionJsonContext.Default.ReleaseCatalog, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async ValueTask CompleteReleaseAsync(string tenant, string product, string version,
        IReadOnlyCollection<string> requiredRids, ReleaseBundleBootstrap? bundle, IReadOnlyList<string> postInstallArguments,
        string promoteChannel, Version hostVersion, Version frameworkVersion, CancellationToken cancellationToken)
    {
        Validate(tenant); Validate(product); Validate(version); Validate(promoteChannel);
        var gate = _locks.GetOrAdd($"release:{tenant}:{product}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var catalog = await GetReleaseCatalogAsync(tenant, product, cancellationToken) ?? throw new KeyNotFoundException("Product catalog not found.");
            var release = catalog.Releases.FirstOrDefault(item => item.Version.Equals(version, StringComparison.OrdinalIgnoreCase)) ?? throw new KeyNotFoundException("Release not found.");
            if (catalog.Revocations.Any(revoked => revoked.Version?.Equals(version, StringComparison.OrdinalIgnoreCase) == true || release.Artifacts.Any(artifact => revoked.Sha256?.Equals(artifact.Sha256, StringComparison.OrdinalIgnoreCase) == true)))
                throw new InvalidOperationException("A revoked release or artifact cannot be promoted.");
            var missing = requiredRids.Where(rid => !release.Artifacts.Any(item => item.RuntimeIdentifier.Equals(rid, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (missing.Length > 0) throw new InvalidOperationException($"Release is missing required artifacts: {string.Join(", ", missing)}.");
            if (bundle is not null)
            {
                var expected = PublicBundlePath(tenant, product, bundle);
                if (!File.Exists(expected)) throw new InvalidOperationException("Bundle manifest is not owned by this registry or does not exist.");
                using var stream = File.OpenRead(expected);
                var digest = Convert.ToHexString(SHA256.HashData(stream));
                var expectedDigest = bundle.Sha256.Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("-", string.Empty);
                if (!digest.Equals(expectedDigest, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Bundle digest does not match registry content.");
                if (postInstallArguments.Count == 0) throw new InvalidOperationException("A bundled release requires a post-install setup contract.");
                await ValidateBundleAsync(expected, tenant, requiredRids, hostVersion, frameworkVersion, cancellationToken);
            }
            release.Bundle = bundle; release.PostInstallArguments.Clear(); release.PostInstallArguments.AddRange(postInstallArguments);
            catalog.Channels[promoteChannel] = version;
            catalog.Rollouts.RemoveAll(item => item.Channel.Equals(promoteChannel, StringComparison.OrdinalIgnoreCase));
            await WriteJsonAtomicAsync(ReleaseCatalogPath(tenant, product), catalog, DistributionJsonContext.Default.ReleaseCatalog, cancellationToken);
        }
        finally { gate.Release(); }
    }

    private async ValueTask ValidateBundleAsync(string path, string tenant, IReadOnlyCollection<string> requiredRids,
        Version hostVersion, Version frameworkVersion, CancellationToken cancellationToken)
    {
        await using var input = File.OpenRead(path);
        var bundle = await JsonSerializer.DeserializeAsync(input, ModuleJsonContext.Default.ModuleBundle, cancellationToken)
            ?? throw new InvalidOperationException("Bundle manifest is empty.");
        if (!ModuleCompatibility.IsCompatible(bundle.HostCompatibility, hostVersion) || !ModuleCompatibility.IsCompatible(bundle.FrameworkCompatibility, frameworkVersion))
            throw new InvalidOperationException("Bundle is incompatible with the declared host or framework version.");
        var catalog = await GetModuleCatalogAsync(tenant, cancellationToken) ?? throw new InvalidOperationException("Owned module catalog does not exist.");
        foreach (var member in bundle.Modules ?? [])
        {
            if (!member.Required) continue;
            foreach (var rid in requiredRids)
                if (!ModuleCompatibility.IsCompatible(member, rid, out var reason)) throw new InvalidOperationException($"Required module '{member.Id}' is ineligible for {rid}: {reason}");
            ModuleRelease selected;
            try { selected = ModuleCatalogClient.SelectRelease(catalog, member.Id, string.IsNullOrWhiteSpace(member.Channel) ? "stable" : member.Channel, member.Version); }
            catch (Exception exception) { throw new InvalidOperationException($"Required module '{member.Id}' is not present in the owned catalog.", exception); }
            if (selected.Revoked) throw new InvalidOperationException($"Required module '{member.Id}' is revoked.");
            var expected = member.Sha256.Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("-", string.Empty);
            var actual = selected.Sha256.Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("-", string.Empty);
            if (!expected.Equals(actual, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"Required module '{member.Id}' digest does not match its catalog release.");
        }
    }

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

    private async ValueTask<(string Digest, long Size)> WriteBlobAsync(string path, Stream content, CancellationToken cancellationToken, bool immutable = false)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".upload-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var output = File.Create(temporary))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > options.MaxUploadBytes) throw new RegistryUploadTooLargeException(options.MaxUploadBytes);
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            string digest;
            long size;
            await using (var verification = File.OpenRead(temporary))
            {
                digest = Convert.ToHexString(await SHA256.HashDataAsync(verification, cancellationToken));
                size = verification.Length;
            }
            if (immutable && File.Exists(path))
            {
                await using var existing = File.OpenRead(path);
                var existingDigest = Convert.ToHexString(await SHA256.HashDataAsync(existing, cancellationToken));
                if (!existingDigest.Equals(digest, StringComparison.OrdinalIgnoreCase))
                    throw new BundleSnapshotConflictException();
            }
            else File.Move(temporary, path, true);
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
    private string PublicBundlePath(string tenant, string product, ReleaseBundleBootstrap bundle)
    {
        var name = bundle.Manifest.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (name != bundle.Snapshot + ".json") throw new InvalidOperationException("Bundle URL does not match its snapshot.");
        return BlobPath(tenant, "products", product, "bundles", name);
    }
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

public sealed record BundleSnapshotResult(Uri Manifest, string Sha256, long Size, string Snapshot);
public sealed class BundleSnapshotConflictException : Exception
{
    public BundleSnapshotConflictException() : base("An immutable bundle snapshot already exists with different content.") { }
}
