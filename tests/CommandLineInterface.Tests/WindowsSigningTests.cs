using CoreVar.CommandLineInterface.Publishing;

namespace CommandLineInterface.Tests;

public sealed class WindowsSigningTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "cli-signing-tests", Guid.NewGuid().ToString("N"));
    private static readonly Uri Timestamp = new("https://timestamp.example.test/");

    [Fact]
    public void CertificateStoreRemainsSupported()
    {
        var arguments = WindowsInstaller.SigningArguments("product.exe", new WindowsSigningOptions
        { CertificateThumbprint = "ABCDEF1234", TimestampUrl = Timestamp, MachineStore = true });
        Assert.Contains("/sha1", arguments);
        Assert.Contains("/sm", arguments);
        Assert.DoesNotContain("/dlib", arguments);
    }

    [Fact]
    public void RejectsAmbiguousOrMissingProvider()
    {
        Assert.Throws<ArgumentException>(() => WindowsInstaller.SigningArguments("product.exe",
            new WindowsSigningOptions { TimestampUrl = Timestamp }));
        Assert.Throws<ArgumentException>(() => WindowsInstaller.SigningArguments("product.exe",
            new WindowsSigningOptions { TimestampUrl = Timestamp, CertificateThumbprint = "ABCD", AzureSigningMetadata = "metadata.json" }));
    }

    [Fact]
    public void AzureProviderRequiresReviewedIdentityAndValidMetadata()
    {
        Directory.CreateDirectory(root);
        var dlib = Path.Combine(root, "client library.dll"); File.WriteAllText(dlib, "fixture");
        var metadata = Path.Combine(root, "metadata.json");
        File.WriteAllText(metadata, """{"Endpoint":"https://signing.example.test/","CodeSigningAccountName":"shared-account","CertificateProfileName":"public-profile"}""");
        Assert.Throws<ArgumentException>(() => WindowsInstaller.SigningArguments("product.exe", new WindowsSigningOptions
        { TimestampUrl = Timestamp, AzureSigningDlib = dlib, AzureSigningMetadata = metadata }));
        var options = new WindowsSigningOptions
        { TimestampUrl = Timestamp, AzureSigningDlib = dlib, AzureSigningMetadata = metadata, PublisherSubject = "CN=Reviewed fixture publisher" };
        var arguments = WindowsInstaller.SigningArguments("product.exe", options);
        Assert.Contains(Path.GetFullPath(dlib), arguments);
        Assert.Contains(Path.GetFullPath(metadata), arguments);
        Assert.Contains("/dmdf", arguments);
        Assert.DoesNotContain("/sha1", arguments);
        File.WriteAllText(metadata, """{"Endpoint":"http://signing.example.test/","CodeSigningAccountName":"shared-account","CertificateProfileName":"public-profile"}""");
        Assert.Throws<ArgumentException>(() => WindowsInstaller.SigningArguments("product.exe", options));
    }

    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
