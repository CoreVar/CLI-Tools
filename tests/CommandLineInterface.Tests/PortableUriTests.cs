using System.Text.Json;
using CoreVar.CommandLineInterface.Distribution;
using CoreVar.CommandLineInterface.Modules;

namespace CommandLineInterface.Tests;

public sealed class PortableUriTests
{
    [Theory]
    [InlineData("file:///tmp/release%20folder/payload%231.zip")]
    [InlineData("C:\\release folder\\payload#1.zip")]
    [InlineData("https://example.test/releases/payload%231.zip?token=a%2Fb")]
    public void CatalogsAndInstallationStatePreserveAbsoluteUris(string path)
    {
        var uri = new Uri(path, UriKind.Absolute);
        var catalog = new ReleaseCatalog { Product = "sample", Releases = [new ReleaseManifest
        {
            Version = "1.0.0", Artifacts = [new ReleaseArtifact { RuntimeIdentifier = "any", Uri = uri, Sha256 = "00" }]
        }] };
        var json = JsonSerializer.Serialize(catalog, DistributionJsonContext.Default.ReleaseCatalog);
        var restored = JsonSerializer.Deserialize(json, DistributionJsonContext.Default.ReleaseCatalog)!;
        AssertUri(uri, restored.Releases[0].Artifacts[0].Uri);

        var state = new InstallationState { Product = "sample", Version = "1.0.0", Catalog = uri };
        var restoredState = JsonSerializer.Deserialize(JsonSerializer.Serialize(state, DistributionJsonContext.Default.InstallationState), DistributionJsonContext.Default.InstallationState)!;
        AssertUri(uri, restoredState.Catalog);

        var modules = new ModuleCatalog { Modules = [new ModuleCatalogEntry { Id = "sample", Releases = [new ModuleRelease { Version = "1.0.0", Package = uri, Sha256 = "00" }] }] };
        var restoredModules = JsonSerializer.Deserialize(JsonSerializer.Serialize(modules, ModuleJsonContext.Default.ModuleCatalog), ModuleJsonContext.Default.ModuleCatalog)!;
        AssertUri(uri, restoredModules.Modules[0].Releases[0].Package);
    }

    [Fact]
    public void NativeFilePathsRoundTripWithEscapedCharacters()
        => CatalogsAndInstallationStatePreserveAbsoluteUris(Path.Combine(Path.GetTempPath(), "release folder", "payload#1.zip"));

    [Fact]
    public void BundleCatalogRetainsItsRelativeReference()
    {
        var bundle = new ModuleBundle { Id = "suite", Snapshot = "one", Catalog = new Uri("../catalog.json", UriKind.Relative) };
        var restored = JsonSerializer.Deserialize(JsonSerializer.Serialize(bundle, ModuleJsonContext.Default.ModuleBundle), ModuleJsonContext.Default.ModuleBundle)!;
        Assert.False(restored.Catalog!.IsAbsoluteUri);
        Assert.Equal("../catalog.json", restored.Catalog.OriginalString);
    }

    private static void AssertUri(Uri expected, Uri actual)
    {
        Assert.True(actual.IsAbsoluteUri);
        Assert.Equal(expected.AbsoluteUri, actual.AbsoluteUri);
        if (expected.IsFile) Assert.Equal(expected.LocalPath, actual.LocalPath);
    }
}
