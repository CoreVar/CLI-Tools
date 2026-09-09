using System.Security.Cryptography;
using System.Text;

namespace CoreVar.CommandLineInterface.Distribution;

public sealed class ArtifactVerifier(IReadOnlyDictionary<string, string>? trustedPublicKeys = null, bool requireSignature = false)
{
    public async ValueTask VerifyAsync(string path, ReleaseArtifact artifact, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var digestBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        var actual = Convert.ToHexString(digestBytes);
        var expected = artifact.Sha256.Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("-", string.Empty);
        if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The release artifact SHA-256 digest did not match the catalog.");
        if (string.IsNullOrWhiteSpace(artifact.Signature))
        {
            if (requireSignature || artifact.SigningKeyId is not null)
                throw new CryptographicException("A trusted release signature is required.");
            return;
        }
        if (artifact.SigningKeyId is null || trustedPublicKeys is null || !trustedPublicKeys.TryGetValue(artifact.SigningKeyId, out var publicKey))
            throw new CryptographicException($"Signing key '{artifact.SigningKeyId}' is not trusted.");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(publicKey);
        byte[] signature;
        try { signature = Convert.FromBase64String(artifact.Signature); }
        catch (FormatException exception) { throw new CryptographicException("The release signature is not valid Base64.", exception); }
        if (!rsa.VerifyData(Encoding.ASCII.GetBytes(actual), signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss))
            throw new CryptographicException("The release artifact signature is invalid.");
    }
}
