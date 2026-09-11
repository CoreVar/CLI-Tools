using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CoreVar.CommandLineInterface.Distribution;

namespace CommandLineInterface.Tests;

public sealed class DistributionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "corevar-distribution-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task DirectUpdateActivatesAndRollsBackVersion()
    {
        Directory.CreateDirectory(_root);
        var archive = Path.Combine(_root, "release.zip");
        using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create)) zip.CreateEntry("sample.exe");
        await using var archiveStream = File.OpenRead(archive);
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(archiveStream));
        var rid = $"{(OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux")}-{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}";
        var catalog = new ReleaseCatalog
        {
            Product = "sample", Channels = { ["stable"] = "2.0.0" },
            Releases = [new ReleaseManifest { Version = "2.0.0", Artifacts = [new ReleaseArtifact { RuntimeIdentifier = rid, Uri = FileUri(archive), Sha256 = digest }] }]
        };
        var catalogPath = Path.Combine(_root, "catalog.json");
        await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(catalog));
        var state = new InstallationState { Product = "sample", Version = "1.0.0", Catalog = FileUri(catalogPath), Provider = InstallationProvider.Direct };
        Directory.CreateDirectory(Path.Combine(_root, "install", "versions", "1.0.0"));
        var updater = new DirectUpdater(new DistributionPaths(Path.Combine(_root, "install")), new ReleaseCatalogClient(), new ArtifactVerifier());

        var updated = await updater.UpdateAsync(state);
        Assert.Equal("2.0.0", updated.Version);
        Assert.True(File.Exists(Path.Combine(_root, "install", "versions", "2.0.0", "sample.exe")));
        Assert.Equal("1.0.0", (await updater.RollbackAsync(updated)).Version);
    }

    private static Uri FileUri(string path) => new UriBuilder(Uri.UriSchemeFile, string.Empty) { Path = Path.GetFullPath(path) }.Uri;

    [Theory]
    [InlineData(InstallationProvider.DotNetTool, "dotnet")]
    [InlineData(InstallationProvider.WinGet, "winget")]
    [InlineData(InstallationProvider.Homebrew, "brew")]
    public void NativeProviderOwnsItsUpdate(InstallationProvider provider, string executable)
    {
        var state = new InstallationState { Product = "sample", Version = "1.0.0", Catalog = new Uri("https://example.invalid/catalog.json"), Provider = provider, PackageId = "sample" };
        Assert.Equal(executable, NativeUpdateCommands.Create(state)!.Executable);
    }

    [Fact]
    public void StagedRolloutIsDeterministicPerInstallation()
    {
        var catalog = new ReleaseCatalog
        {
            Product = "sample", Channels = { ["stable"] = "2.0.0" },
            Releases = [new ReleaseManifest { Version = "1.0.0" }, new ReleaseManifest { Version = "2.0.0" }],
            Rollouts = [new ChannelRollout { Channel = "stable", Version = "2.0.0", FallbackVersion = "1.0.0", Percentage = 50, Seed = "release-2" }]
        };
        var first = ReleaseCatalogClient.Resolve(catalog, "stable", installationId: "device-a").Version;
        Assert.Equal(first, ReleaseCatalogClient.Resolve(catalog, "stable", installationId: "device-a").Version);
        Assert.Contains(first, new[] { "1.0.0", "2.0.0" });
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
