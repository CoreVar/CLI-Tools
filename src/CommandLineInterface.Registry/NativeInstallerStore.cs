using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using CoreVar.CommandLineInterface.Distribution;

namespace CoreVar.CommandLineInterface.Registry;

public sealed partial class FileRegistryStore
{
    public async ValueTask<NativeInstallerRelease> PublishNativeInstallerAsync(string tenant, string product,
        string version, string rid, Stream content, Uri publicUri, string? signature, string? keyId, CancellationToken token)
    {
        Validate(tenant); Validate(product); Validate(version); Validate(rid);
        var keys = options.TrustedSigningKeys.GetValueOrDefault($"{tenant}/{product}");
        if (keys is null || keys.Count == 0 || string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(keyId))
            throw new CryptographicException("Native installers always require an enrolled publisher proof key and signature.");
        var gate = _locks.GetOrAdd($"native:{tenant}:{product}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        try
        {
            if (File.Exists(NativeRevocationPath(tenant, product, version, rid))) throw new InvalidOperationException("Native installer version is revoked.");
            var blob = NativeArtifactPath(tenant, product, version, rid);
            var verifier = new ArtifactVerifier(keys, requireSignature: true);
            var (sha, size) = await WriteBlobAsync(blob, content, token, immutable: true, validate: async (path, digest) =>
            {
                var release = new NativeInstallerRelease(1, product, rid, version, new(publicUri, digest, new FileInfo(path).Length));
                ValidateNativeIdentity(release);
                await verifier.VerifyAsync(path, new ReleaseArtifact { RuntimeIdentifier = rid, Uri = publicUri,
                    Sha256 = digest, Signature = signature, SigningKeyId = keyId }, token);
                RequireEmbeddedSignature(path);
            });
            var result = new NativeInstallerRelease(1, product, rid, version, new(publicUri, sha, size));
            var metadata = NativeMetadataPath(tenant, product, version, rid);
            var existing = await ReadNativeAsync(metadata, token);
            if (existing is not null && existing.Release != result) throw new BundleSnapshotConflictException();
            if (existing is null) await WriteJsonAtomicAsync(metadata, new NativeStoredRelease(result, signature, keyId), null, token);
            return result;
        }
        finally { gate.Release(); }
    }

    public async ValueTask PromoteNativeInstallerAsync(string tenant, string product, string version, string rid,
        string channel, CancellationToken token)
    {
        Validate(channel);
        var gate = _locks.GetOrAdd($"native:{tenant}:{product}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        try
        {
            var saved = await ReadNativeAsync(NativeMetadataPath(tenant, product, version, rid), token)
                ?? throw new KeyNotFoundException("Native installer version does not exist.");
            if (File.Exists(NativeRevocationPath(tenant, product, version, rid))) throw new InvalidOperationException("Native installer is revoked.");
            ValidateNativeIdentity(saved.Release);
            var blob = NativeArtifactPath(tenant, product, version, rid);
            await new ArtifactVerifier(options.TrustedSigningKeys.GetValueOrDefault($"{tenant}/{product}"), true)
                .VerifyAsync(blob, new ReleaseArtifact { RuntimeIdentifier = rid, Uri = saved.Release.Installer.Url,
                    Sha256 = saved.Release.Installer.Sha256, Signature = saved.Signature, SigningKeyId = saved.SigningKeyId }, token);
            RequireEmbeddedSignature(blob);
            await WriteJsonAtomicAsync(NativeChannelPath(tenant, product, channel, rid), saved, null, token);
        }
        finally { gate.Release(); }
    }

    public async ValueTask<NativeInstallerRelease?> GetNativeInstallerFeedAsync(string tenant, string product,
        string channel, string rid, CancellationToken token)
    {
        var saved = await ReadNativeAsync(NativeChannelPath(tenant, product, channel, rid), token);
        if (saved is null || options.TrustedSigningKeys.GetValueOrDefault($"{tenant}/{product}")?.ContainsKey(saved.SigningKeyId) != true ||
            File.Exists(NativeRevocationPath(tenant, product, saved.Release.Version, rid))) return null;
        ValidateNativeIdentity(saved.Release);
        return saved.Release;
    }

    public async ValueTask<string?> GetNativeInstallerPathAsync(string tenant, string product, string version,
        string rid, CancellationToken token)
    {
        var saved = await ReadNativeAsync(NativeMetadataPath(tenant, product, version, rid), token);
        if (saved is null || options.TrustedSigningKeys.GetValueOrDefault($"{tenant}/{product}")?.ContainsKey(saved.SigningKeyId) != true ||
            File.Exists(NativeRevocationPath(tenant, product, version, rid))) return null;
        var path = NativeArtifactPath(tenant, product, version, rid);
        return File.Exists(path) ? path : null;
    }

    public async ValueTask RevokeNativeInstallerAsync(string tenant, string product, string version, string rid,
        CancellationToken token)
    {
        var gate = _locks.GetOrAdd($"native:{tenant}:{product}", _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(token);
        try
        {
            if (await ReadNativeAsync(NativeMetadataPath(tenant, product, version, rid), token) is null)
                throw new KeyNotFoundException("Native installer version does not exist.");
            await WriteJsonAtomicAsync(NativeRevocationPath(tenant, product, version, rid), new { revokedAt = DateTimeOffset.UtcNow }, null, token);
        }
        finally { gate.Release(); }
    }

    // The Linux registry validates publisher-owned RSA proof and signature-table presence.
    // Actual Windows chain, timestamp and publisher/product checks remain mandatory in
    // the Windows publishing client and again in every native update client.
    private static void RequireEmbeddedSignature(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
            var table = pe.PEHeaders.PEHeader?.CertificateTableDirectory;
            if (table is null || table.Value.Size < 9 || table.Value.RelativeVirtualAddress < 0 ||
                (long)table.Value.RelativeVirtualAddress + table.Value.Size > stream.Length)
                throw new InvalidDataException("Native installers must contain an embedded Authenticode signature.");
            stream.Position = table.Value.RelativeVirtualAddress;
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            var length = reader.ReadUInt32(); var revision = reader.ReadUInt16(); var type = reader.ReadUInt16();
            if (length < 9 || length > table.Value.Size || revision != 0x0200 || type != 0x0002)
                throw new InvalidDataException("Native installer Authenticode table is invalid.");
        }
        catch (BadImageFormatException exception) { throw new InvalidDataException("Native installer is not a signed Windows PE.", exception); }
    }

    private static void ValidateNativeIdentity(NativeInstallerRelease release) => NativeInstallerUpdateClient.ValidateRelease(release,
        new(release.Installer.Url, "publisher proof is verified separately", release.ProductId, release.Architecture, "publisher owned", "publisher owned"));
    private static async ValueTask<NativeStoredRelease?> ReadNativeAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<NativeStoredRelease>(stream, cancellationToken: token);
    }
    private string NativeRoot(string tenant, string product) => Path.Combine(Tenant(tenant), "native", Safe(product));
    private string NativeArtifactPath(string tenant, string product, string version, string rid) => Path.Combine(NativeRoot(tenant, product), "versions", Safe(version), Safe(rid), "setup.exe");
    private string NativeMetadataPath(string tenant, string product, string version, string rid) => Path.Combine(NativeRoot(tenant, product), "versions", Safe(version), Safe(rid), "release.json");
    private string NativeRevocationPath(string tenant, string product, string version, string rid) => Path.Combine(NativeRoot(tenant, product), "versions", Safe(version), Safe(rid), "revoked.json");
    private string NativeChannelPath(string tenant, string product, string channel, string rid) => Path.Combine(NativeRoot(tenant, product), "channels", Safe(channel), Safe(rid) + ".json");
    private sealed record NativeStoredRelease(NativeInstallerRelease Release, string Signature, string SigningKeyId);
}
