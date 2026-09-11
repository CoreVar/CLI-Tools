using System.Security.Cryptography;
using CoreVar.CommandLineInterface.Publishing;
using CoreVar.CommandLineInterface.Registry;
using Xunit;

namespace CommandLineInterface.Registry.Tests;

public sealed class SigningPolicyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "registry-signing-" + Guid.NewGuid().ToString("N"));
    [Fact]
    public async Task SignedChannelRejectsUnsignedPromotionAndAcceptsOwnedKey()
    {
        Directory.CreateDirectory(_root);
        var keys = ArtifactSigning.CreateRsaKeyPair();
        var store = new FileRegistryStore(new RegistryOptions { DataRoot = _root, SignedChannels = ["stable"],
            TrustedSigningKeys = new() { ["community/sample"] = new() { ["publisher"] = keys.PublicKeyPem } } });
        var uri = new Uri("https://registry.example/sample.zip");
        await store.PublishReleaseAsync("community", "sample", "1.0.0", "win-x64", "candidate", new MemoryStream([1]), uri, default);
        await Assert.ThrowsAsync<CryptographicException>(() => store.PromoteAsync("community", "sample", "stable", "1.0.0", 100, null, "seed", default).AsTask());
        var file = Path.Combine(_root, "artifact"); await File.WriteAllBytesAsync(file, [1]);
        var signature = await ArtifactSignature.CreateAsync(file, "publisher", keys.PrivateKeyPem);
        await store.PublishReleaseAsync("community", "sample", "1.0.0", "win-x64", "candidate", new MemoryStream([1]), uri, default, signature.Signature, signature.KeyId);
        await store.CompleteReleaseAsync("community", "sample", "1.0.0", ["win-x64"], null, [], "stable", new(1, 0), new(10, 0), default);
        Assert.Equal("1.0.0", (await store.GetReleaseCatalogAsync("community", "sample", default))!.Channels["stable"]);
        await Assert.ThrowsAsync<CryptographicException>(() => store.PublishReleaseAsync("other", "sample", "1.0.0", "win-x64", "candidate", new MemoryStream([1]), uri, default, signature.Signature, signature.KeyId).AsTask());
        Assert.False(File.Exists(store.GetBlobPath("other", "products", "sample", "1.0.0", "win-x64.zip")));
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
