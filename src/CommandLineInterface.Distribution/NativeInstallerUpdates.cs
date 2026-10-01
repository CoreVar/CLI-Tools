using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CoreVar.CommandLineInterface.Distribution;

/// <summary>Publisher-owned trust configuration compiled into the consuming application.</summary>
public sealed record NativeInstallerUpdatePolicy(Uri ManifestUrl, string PublisherSubject, string ProductId,
    string Architecture, string ProductName, string CompanyName);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NativeInstallerArtifact(Uri Url, string Sha256, long SizeBytes);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record NativeInstallerRelease(int SchemaVersion, string ProductId, string Architecture, string Version,
    NativeInstallerArtifact Installer);
public interface INativeInstallerVerifier
{
    void Verify(string path, NativeInstallerRelease release, NativeInstallerUpdatePolicy policy);
}

/// <summary>Downloads bounded native updates. The caller's compiled policy pins the publisher and release origin.</summary>
public sealed class NativeInstallerUpdateClient : IDisposable
{
    public const long MaximumInstallerBytes = 768L * 1024 * 1024;
    public const int MaximumManifestBytes = 16 * 1024;
    private readonly NativeInstallerUpdatePolicy policy;
    private readonly INativeInstallerVerifier verifier;
    private readonly HttpClient http;

    public NativeInstallerUpdateClient(NativeInstallerUpdatePolicy policy, INativeInstallerVerifier verifier,
        HttpMessageHandler? handler = null)
    {
        ValidatePolicy(policy);
        this.policy = policy; this.verifier = verifier;
        http = new HttpClient(handler ?? new SocketsHttpHandler { AllowAutoRedirect = false })
        { Timeout = TimeSpan.FromMinutes(10) };
    }

    public async Task<NativeInstallerRelease?> CheckAsync(Version installed, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(policy.ManifestUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        RequireResponse(response, policy.ManifestUrl);
        using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var content = new MemoryStream();
        await CopyBoundedAsync(source, content, MaximumManifestBytes, cancellationToken);
        var release = JsonSerializer.Deserialize(content.ToArray(), NativeInstallerJsonContext.Default.NativeInstallerRelease)
            ?? throw new InvalidDataException("The native update manifest is empty.");
        ValidateRelease(release, policy);
        return System.Version.Parse(release.Version) > installed ? release : null;
    }

    public async Task<string> DownloadAsync(NativeInstallerRelease release, string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        ValidateRelease(release, policy);
        var directory = Path.Combine(Path.GetFullPath(cacheDirectory), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "setup.exe");
        try
        {
            using var response = await http.GetAsync(release.Installer.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            RequireResponse(response, release.Installer.Url);
            if (response.Content.Headers.ContentLength is { } length && length != release.Installer.SizeBytes)
                throw new InvalidDataException("Native installer length does not match its manifest.");
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            {
                await CopyBoundedAsync(source, output, release.Installer.SizeBytes, cancellationToken);
                if (output.Length != release.Installer.SizeBytes) throw new InvalidDataException("Native installer download is incomplete.");
            }
            VerifyDownloaded(path, release);
            return path;
        }
        catch { File.Delete(path); Directory.Delete(directory); throw; }
    }

    public void VerifyDownloaded(string path, NativeInstallerRelease release)
    {
        using var file = OpenVerified(path, release);
    }

    /// <summary>Holds the verified bytes read-only during native process creation; no silent installer switches.</summary>
    public System.Diagnostics.Process LaunchVerified(string path, NativeInstallerRelease release)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Native Windows installer launch requires Windows.");
        using var file = OpenVerified(path, release);
        return System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.GetFullPath(path))
        { UseShellExecute = false }) ?? throw new InvalidOperationException("The native installer could not start.");
    }

    private FileStream OpenVerified(string path, NativeInstallerRelease release)
    {
        ValidateRelease(release, policy);
        var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (file.Length != release.Installer.SizeBytes ||
                !Convert.ToHexString(SHA256.HashData(file)).Equals(release.Installer.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Native installer bytes do not match the release manifest.");
            verifier.Verify(path, release, policy);
            return file;
        }
        catch { file.Dispose(); throw; }
    }

    public static void ValidatePolicy(NativeInstallerUpdatePolicy policy)
    {
        ValidateHttps(policy.ManifestUrl);
        if (string.IsNullOrWhiteSpace(policy.PublisherSubject) || string.IsNullOrWhiteSpace(policy.ProductId) ||
            policy.Architecture is not ("win-x64" or "win-arm64") ||
            string.IsNullOrWhiteSpace(policy.ProductName) || string.IsNullOrWhiteSpace(policy.CompanyName))
            throw new InvalidDataException("Native updates require a reviewed publisher, product identity and architecture.");
    }

    public static void ValidateRelease(NativeInstallerRelease release, NativeInstallerUpdatePolicy policy)
    {
        ValidatePolicy(policy);
        if (release.SchemaVersion != 1 || release.ProductId != policy.ProductId || release.Architecture != policy.Architecture ||
            !System.Version.TryParse(release.Version, out var version) || version.Major > 255 || version.Minor > 255 ||
            version.Build is < 0 or > 65535 || version.Revision != -1 || release.Installer is null)
            throw new InvalidDataException("Native release identity or three-part installer version is invalid.");
        ValidateHttps(release.Installer.Url);
        if (policy.ManifestUrl.GetLeftPart(UriPartial.Authority) != release.Installer.Url.GetLeftPart(UriPartial.Authority) ||
            !release.Installer.Url.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            release.Installer.SizeBytes is <= 0 or > MaximumInstallerBytes ||
            release.Installer.Sha256 is not { Length: 64 } || release.Installer.Sha256.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException("Native installer must have bounded size, SHA256 and the configured HTTPS origin.");
    }

    public static void ValidateHttps(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri || uri.Scheme != "https" || uri.IsLoopback ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new InvalidDataException("Native updates require public HTTPS without credentials, queries or fragments.");
    }

    private static void RequireResponse(HttpResponseMessage response, Uri expected)
    {
        if (response.StatusCode != HttpStatusCode.OK || response.RequestMessage?.RequestUri != expected)
            throw new HttpRequestException("Native updates require a direct HTTP 200 response; redirects are rejected.");
    }
    private static async Task CopyBoundedAsync(Stream source, Stream target, long maximum, CancellationToken token)
    {
        var buffer = new byte[65536]; long size = 0;
        while (true)
        {
            var count = await source.ReadAsync(buffer, token);
            if (count == 0) return;
            size += count;
            if (size > maximum) throw new InvalidDataException("Native update response exceeds its allowed size.");
            await target.WriteAsync(buffer.AsMemory(0, count), token);
        }
    }
    public void Dispose() => http.Dispose();
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(NativeInstallerRelease))]
[JsonSerializable(typeof(NativeInstallerUpdatePolicy))]
public partial class NativeInstallerJsonContext : JsonSerializerContext;
