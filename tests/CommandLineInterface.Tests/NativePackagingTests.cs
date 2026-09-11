using System.Xml.Linq;
using CoreVar.CommandLineInterface.Publishing;

namespace CommandLineInterface.Tests;

public sealed class NativePackagingTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "native-package-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void PublisherMetadataAndArchitectureArePreserved()
    {
        var winget = NativePackageGenerator.WinGet("Acme.Tool", "Tool", "A & B", "1.0.0", new("https://example.test/app.zip"), new string('A',64), "Apache-2.0", "arm64", "tool.exe");
        Assert.Contains("Apache-2.0", winget); Assert.Contains("Architecture: arm64", winget);
        Assert.DoesNotContain("License: MIT", winget);
        var app = XDocument.Parse(NativePackageGenerator.AppInstaller("Acme.Tool", "1.0.0.0", new("https://example.test/a.msix?x=1&y=2"), publisher: "CN=A & B", architecture: "arm64"));
        var package = app.Root!.Elements().First();
        Assert.Equal("CN=A & B", (string?)package.Attribute("Publisher"));
        Assert.Equal("arm64", (string?)package.Attribute("ProcessorArchitecture"));
    }

    [Fact]
    public void WindowsSourcesHaveStableIdentitiesAndEscapedBranding()
    {
        Directory.CreateDirectory(_root); File.WriteAllText(Path.Combine(_root, "tool.exe"), "fixture");
        var recipe = new WindowsInstallerRecipe { Product = "Acme & Friends", Publisher = "A & B", Version = "1.0.0", SourceDirectory = _root,
            Executable = "tool.exe", UpgradeCode = Guid.NewGuid(), BundleUpgradeCode = Guid.NewGuid(), OutputDirectory = _root + "-out" };
        var first = WindowsInstaller.MsiSource(recipe);
        Assert.Equal(first, WindowsInstaller.MsiSource(recipe));
        Assert.DoesNotContain("PUT-STABLE", first);
        Assert.Equal(recipe.Product, (string?)XDocument.Parse(first).Root!.Elements().Single().Attribute("Name"));
        Assert.Contains("Scope=\"perUser\"", first);
        var bundle = WindowsInstaller.BundleSource(recipe, Path.Combine(_root, "product.msi"));
        Assert.Contains("Compressed=\"yes\"", bundle);
        Assert.Contains("WixStandardBootstrapperApplication", bundle);
    }

    [Fact]
    public void CannotPackIntoThePayloadOrMisidentifyPublisher()
    {
        Directory.CreateDirectory(_root);
        Assert.Throws<ArgumentException>(() => PackageBuilder.Create(_root, Path.Combine(_root, "self.zip")));
        Assert.Throws<ArgumentException>(() => NativePackageGenerator.AppInstaller("product", "1.0.0.0", new("https://example.test/app.msix")));
        Assert.Throws<ArgumentException>(() => InstallerScriptGenerator.PowerShell("bad';product", new("https://example.test/catalog")));
        Assert.Throws<ArgumentException>(() => NativePackageGenerator.DebianControl("safe", "1.0.0", "amd64", "maintainer", "line\nInjected: field"));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
