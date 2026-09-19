using System.Text.Json;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CoreVar.CommandLineInterface.Publishing;

public static class NativePackageGenerator
{
    public static string WinGet(string packageId, string name, string publisher, string version, Uri installer, string sha256, string license = "NOASSERTION", string architecture = "x64", string? executable = null) => $$"""
        PackageIdentifier: {{Yaml(packageId)}}
        PackageVersion: {{Yaml(version)}}
        PackageName: {{Yaml(name)}}
        Publisher: {{Yaml(publisher)}}
        PackageLocale: en-US
        ShortDescription: {{Yaml(name)}}
        License: {{Yaml(license)}}
        InstallerType: zip
        NestedInstallerType: portable
        NestedInstallerFiles:
          - RelativeFilePath: {{Yaml(executable ?? name + ".exe")}}
        Installers:
          - Architecture: {{Architecture(architecture)}}
            InstallerUrl: {{Yaml(installer.AbsoluteUri)}}
            InstallerSha256: {{Digest(sha256)}}
        ManifestType: singleton
        ManifestVersion: 1.6.0
        """;

    public static string Homebrew(string formula, string description, string version, Uri archive, string sha256, string executable, string license = "NOASSERTION") => $$"""
        class {{ClassName(formula)}} < Formula
          desc "{{Ruby(description)}}"
          homepage "{{Ruby(archive.GetLeftPart(UriPartial.Authority))}}"
          url "{{Ruby(archive.AbsoluteUri)}}"
          version "{{Ruby(version)}}"
          sha256 "{{Digest(sha256).ToLowerInvariant()}}"
          license "{{Ruby(license)}}"
          def install
            bin.install "{{Ruby(executable)}}"
          end
        end
        """;

    public static string DebianControl(string package, string version, string architecture, string maintainer, string description) => $$"""
        Package: {{Identifier(package)}}
        Version: {{Identifier(version)}}
        Architecture: {{Identifier(architecture)}}
        Maintainer: {{SingleLine(maintainer)}}
        Description: {{SingleLine(description)}}
        Section: utils
        Priority: optional
        """;

    public static string RpmSpec(string package, string version, string summary, string executable, string license = "NOASSERTION", string architecture = "x86_64") => $$"""
        Name: {{Identifier(package)}}
        Version: {{Identifier(version)}}
        Release: 1%{?dist}
        Summary: {{Line(summary)}}
        License: {{Line(license)}}
        BuildArch: {{Line(architecture)}}
        %description
        {{Line(summary)}}
        %install
        mkdir -p %{buildroot}%{_bindir}
        install -m 0755 {{Identifier(executable)}} %{buildroot}%{_bindir}/{{Identifier(executable)}}
        %files
        %{_bindir}/{{Identifier(executable)}}
        """;

    public static string AppInstaller(string identity, string version, Uri msix, int hoursBetweenChecks = 8,
        string? publisher = null, string architecture = "x64")
    {
        if (string.IsNullOrWhiteSpace(publisher)) throw new ArgumentException("Supply the package certificate publisher distinguished name.", nameof(publisher));
        XNamespace ns = "http://schemas.microsoft.com/appx/appinstaller/2021";
        return new XDocument(new XElement(ns + "AppInstaller", new XAttribute("Version", version), new XAttribute("Uri", msix + ".appinstaller"),
            new XElement(ns + "MainPackage", new XAttribute("Name", identity), new XAttribute("Publisher", publisher), new XAttribute("Version", version),
                new XAttribute("ProcessorArchitecture", Architecture(architecture)), new XAttribute("Uri", msix)),
            new XElement(ns + "UpdateSettings", new XElement(ns + "OnLaunch", new XAttribute("HoursBetweenUpdateChecks", hoursBetweenChecks),
                new XAttribute("ShowPrompt", "true"), new XAttribute("UpdateBlocksActivation", "false"))))) .ToString();
    }

    public static string WixSource(string product, string manufacturer, string version, string executable,
        Guid? upgradeCode = null, string architecture = "x64", string scope = "user") => WindowsInstaller.MsiSource(new WindowsInstallerRecipe
        {
            Product = product, Publisher = manufacturer, Version = version,
            Executable = Path.GetFileName(executable), SourceDirectory = Path.GetDirectoryName(Path.GetFullPath(executable))!,
            OutputDirectory = Path.Combine(Path.GetTempPath(), "cli-installer-output"), Architecture = architecture, Scope = scope,
            UpgradeCode = upgradeCode ?? StableGuid(manufacturer + "/" + product + "/" + scope + "/" + architecture),
            BundleUpgradeCode = StableGuid(manufacturer + "/" + product + "/bundle/" + scope + "/" + architecture)
        });

    private static Guid StableGuid(string name) => new(SHA256.HashData(Encoding.UTF8.GetBytes(name)).AsSpan(0, 16));
    private static string Identifier(string value) => !string.IsNullOrWhiteSpace(value) && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or '+')
        ? value : throw new ArgumentException("Identifier contains unsupported characters.");
    private static string SingleLine(string value) => value.Contains('\n') || value.Contains('\r') ? throw new ArgumentException("Metadata must be a single line.") : value;
    private static string Digest(string value) => value.Length == 64 && value.All(Uri.IsHexDigit) ? value : throw new ArgumentException("SHA-256 must contain 64 hexadecimal characters.");
    private static string Yaml(string value) => '"' + JsonEncodedText.Encode(value).ToString() + '"';
    private static string Ruby(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("#{", "\\#{").Replace("\r", "\\r").Replace("\n", "\\n");
    private static string Line(string value) => value.Contains('\n') || value.Contains('\r') ? throw new ArgumentException("Metadata must be a single line.") : value.Replace("%", "%%");
    private static string Architecture(string value) => value is "x86" or "x64" or "arm64" ? value : throw new ArgumentException("Architecture must be x86, x64 or arm64.");

    private static string ClassName(string value) => string.Concat(Identifier(value).Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries).Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
