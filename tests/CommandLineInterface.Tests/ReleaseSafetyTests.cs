using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CoreVar.CommandLineInterface.Distribution;
using CoreVar.CommandLineInterface.Publishing;
using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Testing;

namespace CommandLineInterface.Tests;

public sealed class ReleaseSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cli-release-safety-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task PublicSelfUpdateApiCanInstallASignedRelease()
    {
        var (_, paths, state) = await CreateUpdateAsync();
        var keys = ArtifactSigning.CreateRsaKeyPair();
        var signature = await ArtifactSignature.CreateAsync(Path.Combine(_root, "release.zip"), "release", keys.PrivateKeyPem);
        var catalog = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(state.Catalog.LocalPath))!;
        var artifact = catalog["releases"]![0]!["artifacts"]![0]!;
        artifact["signature"] = signature.Signature; artifact["signingKeyId"] = signature.KeyId;
        File.WriteAllText(state.Catalog.LocalPath, catalog.ToJsonString());
        var builder = CommandLineBuilder.Create();
        builder.SelfUpdate(new SelfUpdateOptions { Product = state.Product, CurrentVersion = state.Version,
            Catalog = state.Catalog, Root = paths.Root, RequireSignature = true,
            TrustedPublicKeys = new Dictionary<string, string> { ["release"] = keys.PublicKeyPem } });
        await builder.TestAsync("update");
        Assert.Equal("2.0.0", JsonSerializer.Deserialize(File.ReadAllText(paths.State), DistributionJsonContext.Default.InstallationState)!.Version);
    }

    [Fact]
    public async Task SignedStaticReleaseIsUsableWithAnIndependentTrustAnchor()
    {
        Directory.CreateDirectory(_root);
        var input = Path.Combine(_root, "app.zip"); await File.WriteAllTextAsync(input, "artifact");
        var keys = ArtifactSigning.CreateRsaKeyPair();
        var signature = await ArtifactSignature.CreateAsync(input, "publisher-2026", keys.PrivateKeyPem);
        var publisher = new StaticRegistryPublisher(Path.Combine(_root, "site"), new("https://example.test/"));
        var artifact = await publisher.PublishCliAsync("community", "sample", "1.0.0", "win-x64", input, signature: signature);
        var verifier = new ArtifactVerifier(new Dictionary<string, string> { [signature.KeyId] = keys.PublicKeyPem }, true);
        await verifier.VerifyAsync(input, artifact);
        await Assert.ThrowsAsync<CryptographicException>(() => new ArtifactVerifier(requireSignature: true).VerifyAsync(input, artifact).AsTask());
        await File.WriteAllTextAsync(input, "tampered");
        await Assert.ThrowsAsync<InvalidDataException>(() => verifier.VerifyAsync(input, artifact).AsTask());
    }

    [Fact]
    public async Task StaticPublicationCannotOverwritePublishedBytes()
    {
        Directory.CreateDirectory(_root);
        var input = Path.Combine(_root, "app.zip"); await File.WriteAllTextAsync(input, "first");
        var site = Path.Combine(_root, "site");
        var publisher = new StaticRegistryPublisher(site, new("https://example.test/"));
        await publisher.PublishCliAsync("public", "sample", "1.0.0", "win-x64", input);
        var catalog = Path.Combine(site, "v1", "public", "products", "sample", "catalog.json");
        var before = File.ReadAllBytes(catalog);
        await File.WriteAllTextAsync(input, "different");
        await Assert.ThrowsAsync<InvalidDataException>(() => publisher.PublishCliAsync("public", "sample", "1.0.0", "win-x64", input).AsTask());
        Assert.Equal(before, File.ReadAllBytes(catalog));
        Assert.Equal("first", File.ReadAllText(Path.Combine(site, "v1", "public", "blobs", "products", "sample", "1.0.0", "win-x64.zip")));
    }

    [Fact]
    public async Task SignatureCannotBeStrippedWhenRequired()
    {
        Directory.CreateDirectory(_root);
        var input = Path.Combine(_root, "app.zip"); await File.WriteAllTextAsync(input, "artifact");
        var unsigned = new ReleaseArtifact { RuntimeIdentifier = "any", Uri = new(input), Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))) };
        await new ArtifactVerifier().VerifyAsync(input, unsigned); // Free unsigned development remains available.
        await Assert.ThrowsAsync<CryptographicException>(() => new ArtifactVerifier(requireSignature: true).VerifyAsync(input, unsigned).AsTask());
    }

    [Fact]
    public async Task WrongSignatureIsRejectedEvenWhenDigestMatches()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "artifact"); await File.WriteAllTextAsync(path, "artifact");
        var keys = ArtifactSigning.CreateRsaKeyPair();
        var artifact = new ReleaseArtifact { RuntimeIdentifier = "any", Uri = new(path), Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
            SigningKeyId = "trusted", Signature = Convert.ToBase64String(new byte[384]) };
        await Assert.ThrowsAsync<CryptographicException>(() => new ArtifactVerifier(new Dictionary<string, string> { ["trusted"] = keys.PublicKeyPem }, true).VerifyAsync(path, artifact).AsTask());
    }

    [Fact]
    public async Task FailedReadinessPreservesActiveStateAndRetrySucceeds()
    {
        var (updater, paths, state) = await CreateUpdateAsync();
        var original = File.ReadAllBytes(paths.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => updater.UpdateAsync(state, readiness: async (candidate, token) =>
        {
            Assert.Equal("1.0.0", JsonSerializer.Deserialize(File.ReadAllText(paths.State), DistributionJsonContext.Default.InstallationState)!.Version);
            await Assert.ThrowsAsync<IOException>(() => updater.UpdateAsync(state).AsTask());
            return false;
        }).AsTask());
        Assert.Equal(original, File.ReadAllBytes(paths.State));
        var updated = await updater.UpdateAsync(state, readiness: (_, _) => ValueTask.FromResult(true));
        Assert.Equal("2.0.0", updated.Version);
        await Assert.ThrowsAsync<InvalidOperationException>(() => updater.UpdateAsync(state).AsTask());
        Assert.Equal("1.0.0", (await updater.RollbackAsync(updated)).Version);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("/absolute")]
    [InlineData("C:/absolute")]
    public async Task MaliciousArchiveCannotChangeActiveState(string entry)
    {
        var (updater, paths, state) = await CreateUpdateAsync(entry);
        var before = File.ReadAllBytes(paths.State);
        await Assert.ThrowsAsync<InvalidDataException>(() => updater.UpdateAsync(state).AsTask());
        Assert.Equal(before, File.ReadAllBytes(paths.State));
    }

    [Fact]
    public async Task UnixExecutablePermissionsSurviveUpdate()
    {
        if (OperatingSystem.IsWindows()) return;
        var (updater, paths, state) = await CreateUpdateAsync();
        await updater.UpdateAsync(state);
        Assert.True((File.GetUnixFileMode(Path.Combine(paths.Version("2.0.0"), "sample")) & UnixFileMode.UserExecute) != 0);
    }

    private async Task<(DirectUpdater, DistributionPaths, InstallationState)> CreateUpdateAsync(string entryName = "sample")
    {
        Directory.CreateDirectory(_root);
        var archive = Path.Combine(_root, "release.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) { var entry = zip.CreateEntry(entryName); entry.ExternalAttributes = 0x81ED << 16; }
        var rid = $"{(OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux")}-{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}";
        var catalog = new ReleaseCatalog { Product = "sample", Channels = { ["stable"] = "2.0.0" },
            Releases = [new ReleaseManifest { Version = "2.0.0", Artifacts = [new ReleaseArtifact { RuntimeIdentifier = rid, Uri = new(archive), Sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(archive))) }] }] };
        var catalogPath = Path.Combine(_root, "catalog.json");
        await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(catalog, DistributionJsonContext.Default.ReleaseCatalog));
        var paths = new DistributionPaths(Path.Combine(_root, "install"));
        Directory.CreateDirectory(paths.Version("1.0.0"));
        var state = new InstallationState { Product = "sample", Version = "1.0.0", Catalog = new(catalogPath) };
        await File.WriteAllTextAsync(paths.State, JsonSerializer.Serialize(state, DistributionJsonContext.Default.InstallationState));
        return (new(paths, new(), new()), paths, state);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
