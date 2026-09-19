using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Modules;
using CoreVar.CommandLineInterface.Publishing;

namespace CommandLineInterface.Tests;

public sealed class ModuleBundleTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "corevar-bundle-tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("win-x64", true)]
    [InlineData("win-arm64", true)]
    [InlineData("linux-arm64", false)]
    public void CompatibilityFiltersPlatformAndArchitecture(string rid, bool expected)
    {
        var member = new ModuleBundleMember { Id = "sample", Version = "1.0.0", Sha256 = "00", Platforms = ["win"], Architectures = ["x64", "arm64"] };
        Assert.Equal(expected, ModuleCompatibility.IsCompatible(member, rid, out _));
    }

    [Fact]
    public void SparseBundleMemberDefaultsToStableAndAllPlatforms()
    {
        const string json = """
            {"schema":"corevar.cli.bundle/1","id":"suite","snapshot":"one","modules":[{"id":"sample","version":"1.0.0","sha256":"00","platforms":null,"architectures":null,"runtimeIdentifiers":null,"channel":null}]}
            """;
        var bundle = JsonSerializer.Deserialize(json, ModuleJsonContext.Default.ModuleBundle)!;
        var member = Assert.Single(bundle.Modules);
        Assert.True(ModuleCompatibility.IsCompatible(member, "linux-arm64", out _));
        Assert.True(string.IsNullOrWhiteSpace(member.Channel));
    }

    [Fact]
    public async Task LocalPinnedBundleInstallsRequiredModule()
    {
        Directory.CreateDirectory(_root);
        var release = await CreatePackageAsync("sample", "1.0.0");
        var catalogPath = Path.Combine(_root, "catalog.json");
        await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(new ModuleCatalog
        {
            Modules = [new ModuleCatalogEntry { Id = "sample", Releases = [release] }]
        }, ModuleJsonContext.Default.ModuleCatalog));
        var bundlePath = Path.Combine(_root, "bundle.json");
        var bundle = new ModuleBundle
        {
            Id = "suite", Snapshot = "snapshot-1", HostCompatibility = "[0.1.0,0.2.0)",
            Catalog = new Uri("catalog.json", UriKind.Relative),
            Modules = [new ModuleBundleMember { Id = "sample", Version = "1.0.0", Sha256 = release.Sha256 }]
        };
        await File.WriteAllTextAsync(bundlePath, JsonSerializer.Serialize(bundle, ModuleJsonContext.Default.ModuleBundle));
        var digest = await DigestAsync(bundlePath);

        var result = await new ModuleBundleInstaller().InstallAsync(bundlePath, digest,
            new ModuleInstallContext { HostVersion = new(0, 1, 0), FrameworkVersion = new(10, 1, 0), Root = Path.Combine(_root, "distribution") });

        Assert.True(result.IsComplete);
        Assert.Equal(ModuleBundleItemStatus.Installed, Assert.Single(result.Items).Status);
        Assert.NotNull(await new ModuleStore(new ModulePaths(Path.Combine(_root, "distribution"))).GetPointerAsync("sample"));
    }

    [Fact]
    public async Task RequiredFailureNeverReportsComplete()
    {
        Directory.CreateDirectory(_root);
        var catalogPath = Path.Combine(_root, "catalog.json");
        await File.WriteAllTextAsync(catalogPath, JsonSerializer.Serialize(new ModuleCatalog
        {
            Modules = [new ModuleCatalogEntry { Id = "required", Releases = [new ModuleRelease
            { Version = "1.0.0", Package = new Uri("file:///missing.zip"), Sha256 = new string('1', 64) }] }]
        }, ModuleJsonContext.Default.ModuleCatalog));
        var bundlePath = Path.Combine(_root, "bundle.json");
        await File.WriteAllTextAsync(bundlePath, JsonSerializer.Serialize(new ModuleBundle
        {
            Id = "suite", Snapshot = "snapshot-2", Catalog = new Uri("catalog.json", UriKind.Relative),
            Modules = [new ModuleBundleMember { Id = "required", Version = "1.0.0", Sha256 = new string('2', 64), Required = true }]
        }, ModuleJsonContext.Default.ModuleBundle));

        var result = await new ModuleBundleInstaller().InstallAsync(bundlePath, await DigestAsync(bundlePath),
            new ModuleInstallContext { HostVersion = new(0, 1, 0), Root = Path.Combine(_root, "distribution") });

        Assert.False(result.IsComplete);
        Assert.Equal(ModuleBundleItemStatus.Failed, Assert.Single(result.Items).Status);
    }

    private async ValueTask<ModuleRelease> CreatePackageAsync(string id, string version)
    {
        var package = Path.Combine(_root, "module.zip");
        using (var zip = ZipFile.Open(package, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("corevar.module.json");
            {
                await using var stream = entry.Open();
                await JsonSerializer.SerializeAsync(stream, new ModuleManifest
                {
                    Id = id, Version = version, CliCompatibility = "[0.1.0,0.2.0)",
                    Entrypoints = { ["any"] = new ModuleEntrypoint { Path = "module" } },
                    Commands = [new ModuleCommand { Name = "sample" }]
                }, ModuleJsonContext.Default.ModuleManifest);
            }
            zip.CreateEntry("module");
        }
        return new ModuleRelease { Version = version, Package = new Uri(package), Sha256 = await DigestAsync(package) };
    }

    private static async ValueTask<string> DigestAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
