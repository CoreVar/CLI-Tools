using System.Security.Cryptography;
using System.Text;

namespace CoreVar.CommandLineInterface.Publishing;

public static class ArtifactSigning
{
    public static (string PrivateKeyPem, string PublicKeyPem) CreateRsaKeyPair(int size = 3072)
    {
        using var rsa = RSA.Create(size);
        return (rsa.ExportPkcs8PrivateKeyPem(), rsa.ExportSubjectPublicKeyInfoPem());
    }

    public static async ValueTask<string> SignAsync(string artifactPath, string privateKeyPem, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(artifactPath);
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        return Convert.ToBase64String(rsa.SignData(Encoding.ASCII.GetBytes(digest), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }
}
