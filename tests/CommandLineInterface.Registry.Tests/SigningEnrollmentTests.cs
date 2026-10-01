using System.Security.Cryptography;
using System.Text.Json;
using CoreVar.CommandLineInterface.Registry;
using Xunit;

namespace CommandLineInterface.Registry.Tests;

public sealed class SigningEnrollmentTests
{
    [Fact]
    public void AcceptsPublicProductScopedProofKeys()
    {
        using var rsa = RSA.Create(3072);
        var json = JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
        { ["corevar/coredm-station"] = new() { ["dev"] = rsa.ExportSubjectPublicKeyInfoPem() } });
        Assert.Equal(rsa.ExportSubjectPublicKeyInfoPem(), RegistryOptions.ParseSigningKeys(json)["corevar/coredm-station"]["dev"]);
    }

    [Fact]
    public void RejectsPrivateOrWeakKeysAndUnscopedEnrollment()
    {
        using var strong = RSA.Create(2048);
        using var weak = RSA.Create(1024);
        string Json(string scope, string pem) => JsonSerializer.Serialize(new Dictionary<string, Dictionary<string, string>>
        { [scope] = new() { ["dev"] = pem } });
        Assert.Throws<InvalidDataException>(() => RegistryOptions.ParseSigningKeys(Json("corevar/station", strong.ExportPkcs8PrivateKeyPem())));
        Assert.Throws<InvalidDataException>(() => RegistryOptions.ParseSigningKeys(Json("corevar/station", weak.ExportSubjectPublicKeyInfoPem())));
        Assert.Throws<InvalidDataException>(() => RegistryOptions.ParseSigningKeys(Json("corevar", strong.ExportSubjectPublicKeyInfoPem())));
    }
}
