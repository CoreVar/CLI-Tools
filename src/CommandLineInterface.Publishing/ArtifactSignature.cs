using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CoreVar.CommandLineInterface.Distribution;

namespace CoreVar.CommandLineInterface.Publishing;

/// <summary>A portable detached signature. The public key is for publisher-side validation, not client trust enrollment.</summary>
public sealed class ArtifactSignature
{
    public required string KeyId { get; init; }
    public required string Signature { get; init; }
    public required string Sha256 { get; init; }
    public required string PublicKeyPem { get; init; }

    public static async ValueTask<ArtifactSignature> CreateAsync(string path, string keyId, string privateKeyPem, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        await using var input = File.OpenRead(path);
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(input, token));
        return new() { KeyId = keyId, Sha256 = digest, PublicKeyPem = rsa.ExportSubjectPublicKeyInfoPem(),
            Signature = await ArtifactSigning.SignAsync(path, privateKeyPem, token) };
    }

    public async ValueTask VerifyAsync(string path, CancellationToken token = default) =>
        await new ArtifactVerifier(new Dictionary<string, string> { [KeyId] = PublicKeyPem }, true)
            .VerifyAsync(path, new ReleaseArtifact { RuntimeIdentifier = "any", Uri = new Uri(Path.GetFullPath(path)),
                Sha256 = Sha256, Signature = Signature, SigningKeyId = KeyId }, token);

    public static ArtifactSignature Load(string path) => JsonSerializer.Deserialize(File.ReadAllText(path), SignatureJsonContext.Default.ArtifactSignature)
        ?? throw new InvalidDataException("Signature file is empty.");

    public Task SaveAsync(string path, CancellationToken token = default) =>
        File.WriteAllTextAsync(path, JsonSerializer.Serialize(this, SignatureJsonContext.Default.ArtifactSignature), token);
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(ArtifactSignature))]
internal partial class SignatureJsonContext : JsonSerializerContext;
