using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using CoreVar.CommandLineInterface.Distribution;
using CoreVar.CommandLineInterface.Publishing;

namespace CommandLineInterface.Tests;

public sealed class NativeInstallerUpdateTests
{
    private static readonly NativeInstallerUpdatePolicy Policy = new(new Uri("https://registry.example.test/native/stable.json"),
        "CN=Reviewed fixture publisher", "sample", "win-x64", "Sample Setup", "Sample Company");
    private static NativeInstallerRelease Release(byte[] bytes) => new(1, "sample", "win-x64", "1.0.1",
        new(new Uri("https://registry.example.test/native/1.0.1/setup.exe"), Convert.ToHexString(SHA256.HashData(bytes)), bytes.Length));

    [Theory]
    [InlineData("http://registry.example.test/setup.exe")]
    [InlineData("https://other.example.test/setup.exe")]
    [InlineData("https://registry.example.test/setup.exe?token=secret")]
    [InlineData("https://registry.example.test/setup.exe#fragment")]
    public void RejectsUntrustedDownloadUrls(string url)
    {
        var release = Release([1]);
        Assert.Throws<InvalidDataException>(() => NativeInstallerUpdateClient.ValidateRelease(
            release with { Installer = release.Installer with { Url = new Uri(url) } }, Policy));
    }

    [Fact]
    public async Task DownloadVerifiesBytesAndRechecksCachedTampering()
    {
        byte[] bytes = [1, 2, 3]; var release = Release(bytes); var verifier = new CountingVerifier();
        using var client = new NativeInstallerUpdateClient(Policy, verifier, new Handler(request =>
            request.RequestUri == Policy.ManifestUrl ? new StringContent(JsonSerializer.Serialize(release, NativeInstallerJsonContext.Default.NativeInstallerRelease)) : new ByteArrayContent(bytes)));
        Assert.NotNull(await client.CheckAsync(new Version(1, 0, 0)));
        var root = Path.Combine(Path.GetTempPath(), "native-update-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = await client.DownloadAsync(release, root);
            Assert.Equal(1, verifier.Count);
            File.WriteAllBytes(path, [1, 2, 4]);
            Assert.Throws<InvalidDataException>(() => client.VerifyDownloaded(path, release));
            Assert.Equal(1, verifier.Count);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsRedirectsOversizedManifestsAndUnknownFields()
    {
        using var redirect = new NativeInstallerUpdateClient(Policy, new CountingVerifier(), new Handler(_ => new StringContent(""), HttpStatusCode.Redirect));
        await Assert.ThrowsAsync<HttpRequestException>(() => redirect.CheckAsync(new Version(1, 0, 0)));
        using var huge = new NativeInstallerUpdateClient(Policy, new CountingVerifier(), new Handler(_ => new StringContent(new string('x', NativeInstallerUpdateClient.MaximumManifestBytes + 1))));
        await Assert.ThrowsAsync<InvalidDataException>(() => huge.CheckAsync(new Version(1, 0, 0)));
        var json = JsonSerializer.Serialize(Release([1]), NativeInstallerJsonContext.Default.NativeInstallerRelease);
        using var unknown = new NativeInstallerUpdateClient(Policy, new CountingVerifier(), new Handler(_ => new StringContent(json[..^1] + ",\"publisherSubject\":\"attacker\"}")));
        await Assert.ThrowsAsync<JsonException>(() => unknown.CheckAsync(new Version(1, 0, 0)));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task FailedDownloadNeverReachesTrustVerifierAndRemovesItsOwnedCache(int size)
    {
        var verifier = new CountingVerifier(); var release = Release([1, 2, 3]);
        using var client = new NativeInstallerUpdateClient(Policy, verifier, new Handler(_ => new ByteArrayContent(new byte[size])));
        var root = Path.Combine(Path.GetTempPath(), "native-update-" + Guid.NewGuid().ToString("N"));
        try
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => client.DownloadAsync(release, root));
            Assert.Equal(0, verifier.Count);
            Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void NativeTrustVerifierRejectsUnsignedContentOrUnsupportedPlatform()
    {
        var file = Path.GetTempFileName(); File.WriteAllBytes(file, [1, 2, 3]);
        try
        {
            if (OperatingSystem.IsWindows()) Assert.Throws<InvalidDataException>(() => new WindowsNativeInstallerVerifier().Verify(file, Release([1, 2, 3]), Policy));
            else Assert.Throws<PlatformNotSupportedException>(() => new WindowsNativeInstallerVerifier().Verify(file, Release([1, 2, 3]), Policy));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public async Task NativePublisherRejectsUnsignedBytesBeforeAnyHttpRequest()
    {
        var file = Path.GetTempFileName(); File.WriteAllBytes(file, [1, 2, 3]);
        var handler = new CountingHttpHandler(); using var http = new HttpClient(handler);
        try
        {
            var keys = ArtifactSigning.CreateRsaKeyPair();
            var proof = await ArtifactSignature.CreateAsync(file, "publisher", keys.PrivateKeyPem);
            var action = new Func<Task>(async () => await new RegistryPublisher(http).PublishNativeInstallerAsync(
                new Uri("https://registry.example.test/"), "community", "sample", "1.0.1", "win-x64", file, Policy, proof));
            if (OperatingSystem.IsWindows()) await Assert.ThrowsAsync<InvalidDataException>(action);
            else await Assert.ThrowsAsync<PlatformNotSupportedException>(action);
            Assert.Equal(0, handler.Count);
        }
        finally { File.Delete(file); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeMutationsRejectRedirectsAndNonHttpsBeforeSending(bool revoke)
    {
        using var http = new HttpClient(new Handler(_ => new StringContent(""), HttpStatusCode.Redirect));
        var publisher = new RegistryPublisher(http);
        async Task Mutate(Uri endpoint)
        {
            if (revoke) await publisher.RevokeNativeInstallerAsync(endpoint, "community", "sample", "1.0.1", "win-x64");
            else await publisher.PromoteNativeInstallerAsync(endpoint, "community", "sample", "dev", "1.0.1", "win-x64");
        }
        await Assert.ThrowsAsync<HttpRequestException>(() => Mutate(new Uri("https://registry.example.test/")));
        await Assert.ThrowsAsync<InvalidDataException>(() => Mutate(new Uri("http://registry.example.test/")));
    }

    private sealed class CountingHttpHandler : HttpMessageHandler
    {
        public int Count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Count++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)); }
    }
    private sealed class CountingVerifier : INativeInstallerVerifier
    { public int Count; public void Verify(string path, NativeInstallerRelease release, NativeInstallerUpdatePolicy policy) => Count++; }
    private sealed class Handler(Func<HttpRequestMessage, HttpContent> content, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { RequestMessage = request, Content = content(request) });
    }
}
