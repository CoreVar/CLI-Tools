namespace CoreVar.CommandLineInterface.Publishing;

public static class NativePackageGenerator
{
    public static string WinGet(string packageId, string name, string publisher, string version, Uri installer, string sha256) => $$"""
        PackageIdentifier: {{packageId}}
        PackageVersion: {{version}}
        PackageName: {{name}}
        Publisher: {{publisher}}
        License: MIT
        InstallerType: zip
        Installers:
          - Architecture: x64
            InstallerUrl: {{installer}}
            InstallerSha256: {{sha256}}
        ManifestType: singleton
        ManifestVersion: 1.6.0
        """;

    public static string Homebrew(string formula, string description, string version, Uri archive, string sha256, string executable) => $$"""
        class {{ClassName(formula)}} < Formula
          desc "{{description.Replace("\"", "\\\"")}}"
          homepage "{{archive.GetLeftPart(UriPartial.Authority)}}"
          url "{{archive}}"
          version "{{version}}"
          sha256 "{{sha256.ToLowerInvariant()}}"
          license "MIT"
          def install
            bin.install "{{executable}}"
          end
        end
        """;

    public static string DebianControl(string package, string version, string architecture, string maintainer, string description) => $$"""
        Package: {{package}}
        Version: {{version}}
        Architecture: {{architecture}}
        Maintainer: {{maintainer}}
        Description: {{description}}
        Section: utils
        Priority: optional
        """;

    public static string RpmSpec(string package, string version, string summary, string executable) => $$"""
        Name: {{package}}
        Version: {{version}}
        Release: 1%{?dist}
        Summary: {{summary}}
        License: MIT
        %description
        {{summary}}
        %install
        mkdir -p %{buildroot}%{_bindir}
        install -m 0755 {{executable}} %{buildroot}%{_bindir}/{{executable}}
        %files
        %{_bindir}/{{executable}}
        """;

    public static string AppInstaller(string identity, string version, Uri msix, int hoursBetweenChecks = 8) => $$"""
        <?xml version="1.0" encoding="utf-8"?>
        <AppInstaller xmlns="http://schemas.microsoft.com/appx/appinstaller/2021" Version="{{version}}" Uri="{{msix}}.appinstaller">
          <MainPackage Name="{{identity}}" Publisher="CN={{identity}}" Version="{{version}}" ProcessorArchitecture="x64" Uri="{{msix}}" />
          <UpdateSettings><OnLaunch HoursBetweenUpdateChecks="{{hoursBetweenChecks}}" ShowPrompt="true" UpdateBlocksActivation="false" /></UpdateSettings>
        </AppInstaller>
        """;

    public static string WixSource(string product, string manufacturer, string version, string executable) => $$"""
        <Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
          <Package Name="{{product}}" Manufacturer="{{manufacturer}}" Version="{{version}}" UpgradeCode="PUT-STABLE-GUID-HERE">
            <MajorUpgrade DowngradeErrorMessage="A newer version is already installed." />
            <MediaTemplate EmbedCab="yes" />
            <StandardDirectory Id="ProgramFiles64Folder">
              <Directory Id="INSTALLFOLDER" Name="{{product}}">
                <Component Id="ProductExecutable"><File Source="{{executable}}" /></Component>
              </Directory>
            </StandardDirectory>
            <Feature Id="Main"><ComponentRef Id="ProductExecutable" /></Feature>
          </Package>
        </Wix>
        """;

    private static string ClassName(string value) => string.Concat(value.Split('-', '_').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
