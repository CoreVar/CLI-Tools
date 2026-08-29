using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CoreVar.CommandLineInterface.Modules;

namespace CommandLineInterface.Tests;

public sealed class ModuleInstallerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "corevar-module-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InstallsActivatesAndRollsBackImmutableModuleVersions()
    {
        var paths = new ModulePaths(_root);
        var store = new ModuleStore(paths);
        var installer = new ModuleInstaller(paths, store, new ModuleRuntimeProvisioner());
        var first = await CreatePackageAsync("sample", "1.0.0");
        var second = await CreatePackageAsync("sample", "1.1.0");

        await installer.InstallAsync("sample", first);
        await installer.InstallAsync("sample", second);

        Assert.Equal("1.1.0", (await store.GetCurrentAsync("sample"))!.Version);
        Assert.True(await installer.RollbackAsync("sample"));
        Assert.Equal("1.0.0", (await store.GetCurrentAsync("sample"))!.Version);
    }

    [Fact]
    public async Task RejectsPackageWithWrongDigest()
    {
        var paths = new ModulePaths(_root);
        var installer = new ModuleInstaller(paths, new ModuleStore(paths), new ModuleRuntimeProvisioner());
        var release = await CreatePackageAsync("sample", "1.0.0");
        release = new ModuleRelease { Version = release.Version, Package = release.Package, Sha256 = new string('0', 64) };

        await Assert.ThrowsAsync<InvalidDataException>(() => installer.InstallAsync("sample", release).AsTask());
    }

    [Fact]
    public async Task BlocksArchivePathTraversal()
    {
        Directory.CreateDirectory(_root);
        var package = Path.Combine(_root, "evil.zip");
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create)) archive.CreateEntry("../outside.txt");
        await using var stream = File.OpenRead(package);
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(stream));
        var paths = new ModulePaths(Path.Combine(_root, "install"));
        var installer = new ModuleInstaller(paths, new ModuleStore(paths), new ModuleRuntimeProvisioner());

        await Assert.ThrowsAsync<InvalidDataException>(() => installer.InstallAsync("sample",
            new ModuleRelease { Version = "1.0.0", Package = new Uri(package), Sha256 = digest }).AsTask());
        Assert.False(File.Exists(Path.Combine(_root, "install", "modules", "sample", "versions", "outside.txt")));
    }

    [Fact]
    public void ChoosesLatestNonRevokedReleaseInChannel()
    {
        var catalog = new ModuleCatalog { Modules = [new ModuleCatalogEntry { Id = "sample", Releases =
        [
            Release("1.0.0", "stable"), Release("1.1.0-preview.1", "preview"), Release("1.1.0", "stable"),
            new ModuleRelease { Version = "2.0.0", Channel = "stable", Revoked = true, Package = new Uri("file:///revoked.zip"), Sha256 = new string('0', 64) }
        ] }] };

        Assert.Equal("1.1.0", ModuleCatalogClient.SelectRelease(catalog, "sample").Version);
        Assert.Equal("1.1.0-preview.1", ModuleCatalogClient.SelectRelease(catalog, "sample", "preview").Version);
    }

    [Theory]
    [InlineData("[11.0.0,12.0.0)", "11.2.0", true)]
    [InlineData("[11.0.0,12.0.0)", "12.0.0", false)]
    [InlineData("(11.0.0,)", "11.0.0", false)]
    public void EnforcesCliCompatibility(string range, string host, bool expected) =>
        Assert.Equal(expected, ModuleCompatibility.IsCompatible(range, Version.Parse(host)));

    private async ValueTask<ModuleRelease> CreatePackageAsync(string id, string version)
    {
        Directory.CreateDirectory(_root);
        var package = Path.Combine(_root, $"{id}-{version}.zip");
        var manifest = new ModuleManifest
        {
            Id = id, Version = version,
            Entrypoints = { ["any"] = new ModuleEntrypoint { Path = OperatingSystem.IsWindows() ? "module.cmd" : "module" } },
            Commands = [new ModuleCommand { Name = id }]
        };
        using (var archive = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("corevar.module.json");
            await using (var stream = entry.Open())
                await JsonSerializer.SerializeAsync(stream, manifest);
            archive.CreateEntry(OperatingSystem.IsWindows() ? "module.cmd" : "module");
        }
        await using var packageStream = File.OpenRead(package);
        var digest = Convert.ToHexString(await SHA256.HashDataAsync(packageStream));
        return new ModuleRelease { Version = version, Package = new Uri(package), Sha256 = digest };
    }

    private static ModuleRelease Release(string version, string channel) => new()
    {
        Version = version, Channel = channel, Package = new Uri($"file:///{version}.zip"), Sha256 = new string('0', 64)
    };

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
