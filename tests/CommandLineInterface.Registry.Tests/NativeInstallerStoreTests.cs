using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using CoreVar.CommandLineInterface.Publishing;
using CoreVar.CommandLineInterface.Registry;
using Xunit;

namespace CommandLineInterface.Registry.Tests;

public sealed class NativeInstallerStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "native-registry-" + Guid.NewGuid().ToString("N"));
    private static readonly Uri Uri = new("https://registry.example.test/v1/community/native/products/sample/installers/1.0.1/win-x64/setup.exe");

    [Fact]
    public async Task RejectsMissingProofAndUnsignedNativeBytesEvenWithOwnedProof()
    {
        Directory.CreateDirectory(root);
        var keys = ArtifactSigning.CreateRsaKeyPair();
        var store = Store(keys.PublicKeyPem);
        await Assert.ThrowsAsync<CryptographicException>(() => store.PublishNativeInstallerAsync("community", "sample", "1.0.1", "win-x64",
            new MemoryStream([1]), Uri, null, null, default).AsTask());
        var file = Path.Combine(root, "unsigned.dll"); File.Copy(typeof(FileRegistryStore).Assembly.Location, file);
        var proof = await ArtifactSignature.CreateAsync(file, "publisher", keys.PrivateKeyPem);
        await using var input = File.OpenRead(file);
        await Assert.ThrowsAsync<InvalidDataException>(() => store.PublishNativeInstallerAsync("community", "sample", "1.0.1", "win-x64",
            input, Uri, proof.Signature, proof.KeyId, default).AsTask());
        Assert.Null(await store.GetNativeInstallerFeedAsync("community", "sample", "dev", "win-x64", default));
        Assert.Null(await store.GetNativeInstallerPathAsync("community", "sample", "1.0.1", "win-x64", default));
    }

    [Fact]
    public async Task ImmutablePublicationRequiresExplicitPromotionAndRevocationHidesBothFeedAndBlob()
    {
        Directory.CreateDirectory(root);
        var keys = ArtifactSigning.CreateRsaKeyPair(); var store = Store(keys.PublicKeyPem);
        var file = StructuralFixture("fixture.exe");
        var proof = await ArtifactSignature.CreateAsync(file, "publisher", keys.PrivateKeyPem);
        await using (var input = File.OpenRead(file))
            await store.PublishNativeInstallerAsync("community", "sample", "1.0.1", "win-x64", input, Uri, proof.Signature, proof.KeyId, default);
        Assert.Null(await store.GetNativeInstallerFeedAsync("community", "sample", "dev", "win-x64", default));
        await store.PromoteNativeInstallerAsync("community", "sample", "1.0.1", "win-x64", "dev", default);
        Assert.Equal("1.0.1", (await store.GetNativeInstallerFeedAsync("community", "sample", "dev", "win-x64", default))!.Version);
        File.AppendAllText(file, "changed");
        var changed = await ArtifactSignature.CreateAsync(file, "publisher", keys.PrivateKeyPem);
        await using (var input = File.OpenRead(file))
            await Assert.ThrowsAsync<BundleSnapshotConflictException>(() => store.PublishNativeInstallerAsync("community", "sample", "1.0.1", "win-x64", input, Uri, changed.Signature, changed.KeyId, default).AsTask());
        await store.RevokeNativeInstallerAsync("community", "sample", "1.0.1", "win-x64", default);
        Assert.Null(await store.GetNativeInstallerFeedAsync("community", "sample", "dev", "win-x64", default));
        Assert.Null(await store.GetNativeInstallerPathAsync("community", "sample", "1.0.1", "win-x64", default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.PromoteNativeInstallerAsync("community", "sample", "1.0.1", "win-x64", "dev", default).AsTask());
    }

    [Fact]
    public async Task PublisherKeysCannotCrossTenantOrProductBoundaries()
    {
        Directory.CreateDirectory(root);
        var keys = ArtifactSigning.CreateRsaKeyPair(); var store = Store(keys.PublicKeyPem);
        var file = StructuralFixture("fixture.exe"); var proof = await ArtifactSignature.CreateAsync(file, "publisher", keys.PrivateKeyPem);
        await using var input = File.OpenRead(file);
        await Assert.ThrowsAsync<CryptographicException>(() => store.PublishNativeInstallerAsync("other", "sample", "1.0.1", "win-x64", input, Uri, proof.Signature, proof.KeyId, default).AsTask());
        await Assert.ThrowsAsync<CryptographicException>(() => store.PublishNativeInstallerAsync("community", "other", "1.0.1", "win-x64", input, Uri, proof.Signature, proof.KeyId, default).AsTask());
    }

    private FileRegistryStore Store(string publicKey) => new(new RegistryOptions { DataRoot = Path.Combine(root, "data"),
        TrustedSigningKeys = new() { ["community/sample"] = new() { ["publisher"] = publicKey } } });

    // Synthetic signature-table presence fixture tests Linux storage policy, NOT Windows trust.
    // It is never published outside this disposable test directory. Real clients reject it.
    private string StructuralFixture(string name)
    {
        var bytes = File.ReadAllBytes(typeof(FileRegistryStore).Assembly.Location);
        using var input = new MemoryStream(bytes); using var reader = new PEReader(input);
        var offset = reader.PEHeaders.PEHeaderStartOffset + (reader.PEHeaders.PEHeader!.Magic == PEMagic.PE32Plus ? 112 : 96) + 4 * 8;
        var certificate = (bytes.Length + 7) & ~7;
        Array.Resize(ref bytes, certificate + 16);
        BitConverter.GetBytes(certificate).CopyTo(bytes, offset); BitConverter.GetBytes(16).CopyTo(bytes, offset + 4);
        BitConverter.GetBytes(9u).CopyTo(bytes, certificate); BitConverter.GetBytes((ushort)0x0200).CopyTo(bytes, certificate + 4);
        BitConverter.GetBytes((ushort)0x0002).CopyTo(bytes, certificate + 6); bytes[certificate + 8] = 0;
        var path = Path.Combine(root, name); File.WriteAllBytes(path, bytes); return path;
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
