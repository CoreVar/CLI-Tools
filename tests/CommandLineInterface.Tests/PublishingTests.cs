using System.Security.Cryptography;
using CoreVar.CommandLineInterface.Distribution;
using CoreVar.CommandLineInterface.Publishing;

namespace CommandLineInterface.Tests;

public sealed class PublishingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "corevar-publishing-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StaticPublisherCreatesConsumableCatalogAndBlob()
    {
        Directory.CreateDirectory(_root);
        var artifact = Path.Combine(_root, "input.zip"); await File.WriteAllTextAsync(artifact, "artifact");
        var publisher = new StaticRegistryPublisher(Path.Combine(_root, "site"), new Uri("https://example.test/downloads/"));

        var published = await publisher.PublishCliAsync("public", "sample", "1.0.0", "win-x64", artifact);
        var catalogUri = new Uri(Path.Combine(_root, "site", "v1", "public", "products", "sample", "catalog.json"));
        var catalog = await new ReleaseCatalogClient().LoadAsync(catalogUri);

        Assert.Equal("1.0.0", catalog.Channels["stable"]);
        Assert.Equal(published.Sha256, catalog.Releases.Single().Artifacts.Single().Sha256);
        Assert.True(File.Exists(Path.Combine(_root, "site", "v1", "public", "blobs", "products", "sample", "1.0.0", "win-x64.zip")));
    }

    [Fact]
    public async Task RsaSignatureVerifiesArtifactDigest()
    {
        Directory.CreateDirectory(_root);
        var artifact = Path.Combine(_root, "signed.zip"); await File.WriteAllTextAsync(artifact, "signed artifact");
        var keys = ArtifactSigning.CreateRsaKeyPair();
        var signature = await ArtifactSigning.SignAsync(artifact, keys.PrivateKeyPem);
        await using var stream = File.OpenRead(artifact);
        var sha = Convert.ToHexString(await SHA256.HashDataAsync(stream));

        await new ArtifactVerifier(new Dictionary<string, string> { ["release"] = keys.PublicKeyPem })
            .VerifyAsync(artifact, new ReleaseArtifact { RuntimeIdentifier = "any", Uri = new Uri(artifact), Sha256 = sha, Signature = signature, SigningKeyId = "release" });
    }

    [Fact]
    public void PackageBuilderIsReproducible()
    {
        var source = Path.Combine(_root, "source"); Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "app"), "hello");
        var first = Path.Combine(_root, "first.zip"); var second = Path.Combine(_root, "second.zip");
        PackageBuilder.Create(source, first); PackageBuilder.Create(source, second);
        Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
