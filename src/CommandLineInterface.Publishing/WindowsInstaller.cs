using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;
using CoreVar.CommandLineInterface.IO;

namespace CoreVar.CommandLineInterface.Publishing;

/// <summary>Publisher-owned Windows installer configuration. No private keys or passwords belong here.</summary>
public sealed class WindowsInstallerRecipe
{
    public required string Product { get; init; }
    public required string Publisher { get; init; }
    public required string Version { get; init; }
    public required string SourceDirectory { get; set; }
    public required string Executable { get; init; }
    public required Guid UpgradeCode { get; init; }
    public required Guid BundleUpgradeCode { get; init; }
    public string Architecture { get; set; } = "x64";
    public string Scope { get; set; } = "user";
    public string OutputDirectory { get; set; } = "dist/windows";
    public string? LogoFile { get; set; }
    public string? IconFile { get; set; }
    public string? ThemeFile { get; set; }
    public string? LicenseUrl { get; init; }
    public string? SupportUrl { get; init; }
    public Uri? DownloadUrl { get; init; }
    public WindowsSigningOptions? Signing { get; init; }
    public bool RequireSigning { get; init; }

    public static WindowsInstallerRecipe Load(string path)
    {
        var value = JsonSerializer.Deserialize(File.ReadAllText(path), WindowsInstallerJsonContext.Default.WindowsInstallerRecipe)
            ?? throw new InvalidDataException("Installer recipe is empty.");
        var root = Path.GetDirectoryName(Path.GetFullPath(path))!;
        value.Architecture ??= "x64";
        value.Scope ??= "user";
        value.SourceDirectory = Path.GetFullPath(value.SourceDirectory, root);
        value.OutputDirectory = Path.GetFullPath(value.OutputDirectory ?? "dist/windows", root);
        if (value.LogoFile is not null) value.LogoFile = Path.GetFullPath(value.LogoFile, root);
        if (value.IconFile is not null) value.IconFile = Path.GetFullPath(value.IconFile, root);
        if (value.ThemeFile is not null) value.ThemeFile = Path.GetFullPath(value.ThemeFile, root);
        return value;
    }
}

public sealed class WindowsSigningOptions
{
    public required string CertificateThumbprint { get; init; }
    public string Store { get; init; } = "My";
    public bool MachineStore { get; init; }
    public required Uri TimestampUrl { get; init; }
    public string SignTool { get; init; } = "signtool";
}

public static class WindowsInstaller
{
    private static readonly XNamespace Wix = "http://wixtoolset.org/schemas/v4/wxs";
    private static readonly XNamespace Bal = "http://wixtoolset.org/schemas/v4/wxs/bal";

    public static string MsiSource(WindowsInstallerRecipe recipe)
    {
        Validate(recipe);
        var perUser = recipe.Scope == "user";
        var package = new XElement(Wix + "Package", new XAttribute("Name", recipe.Product), new XAttribute("Manufacturer", recipe.Publisher),
            new XAttribute("Version", recipe.Version), new XAttribute("UpgradeCode", recipe.UpgradeCode), new XAttribute("Scope", perUser ? "perUser" : "perMachine"),
            new XElement(Wix + "MajorUpgrade", new XAttribute("DowngradeErrorMessage", "A newer version is already installed."), new XAttribute("Schedule", "afterInstallInitialize")),
            new XElement(Wix + "MediaTemplate", new XAttribute("EmbedCab", "yes")));
        var directory = new XElement(Wix + "Directory", new XAttribute("Id", "INSTALLFOLDER"), new XAttribute("Name", recipe.Product));
        package.Add(new XElement(Wix + "StandardDirectory", new XAttribute("Id", perUser ? "LocalAppDataFolder" : recipe.Architecture == "x86" ? "ProgramFilesFolder" : "ProgramFiles64Folder"), directory));
        var feature = new XElement(Wix + "Feature", new XAttribute("Id", "Main")); package.Add(feature);
        foreach (var file in InstallationFiles.PayloadFiles(recipe.SourceDirectory).OrderBy(x => x, StringComparer.Ordinal))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Installer source links are not supported.");
            var relative = Path.GetRelativePath(recipe.SourceDirectory, file).Replace('\\', '/');
            var parent = directory;
            var prefix = "";
            foreach (var part in relative.Split('/')[..^1])
            {
                prefix += "/" + part;
                var id = Id(prefix);
                var next = parent.Elements(Wix + "Directory").FirstOrDefault(x => (string?)x.Attribute("Id") == id);
                if (next is null) { next = new XElement(Wix + "Directory", new XAttribute("Id", id), new XAttribute("Name", part)); parent.Add(next); }
                parent = next;
            }
            var componentId = Id(relative);
            var componentGuid = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{recipe.UpgradeCode}/{recipe.Scope}/{recipe.Architecture}/{relative}")).AsSpan(0, 16));
            var component = new XElement(Wix + "Component", new XAttribute("Id", componentId), new XAttribute("Guid", componentGuid),
                new XElement(Wix + "File", new XAttribute("Source", Path.GetFullPath(file)), new XAttribute("KeyPath", perUser ? "no" : "yes")));
            if (perUser)
            {
                component.Add(new XElement(Wix + "RegistryValue", new XAttribute("Root", "HKCU"), new XAttribute("Key", $"Software\\CLI-Tools\\{recipe.UpgradeCode:D}"),
                    new XAttribute("Name", componentId), new XAttribute("Type", "integer"), new XAttribute("Value", 1), new XAttribute("KeyPath", "yes")));
                foreach (var ancestor in parent.AncestorsAndSelf().TakeWhile(x => x.Name == Wix + "Directory"))
                    component.Add(new XElement(Wix + "RemoveFolder", new XAttribute("Id", "R" + componentId + (string)ancestor.Attribute("Id")!),
                        new XAttribute("Directory", (string)ancestor.Attribute("Id")!), new XAttribute("On", "uninstall")));
            }
            if (relative.Equals(recipe.Executable.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                component.Add(new XElement(Wix + "Environment", new XAttribute("Id", "ProductPath"), new XAttribute("Name", "PATH"),
                    new XAttribute("Value", "[INSTALLFOLDER]"), new XAttribute("Part", "last"), new XAttribute("Action", "set"), new XAttribute("System", perUser ? "no" : "yes")));
            parent.Add(component); feature.Add(new XElement(Wix + "ComponentRef", new XAttribute("Id", componentId)));
        }
        return new XDocument(new XElement(Wix + "Wix", package)).ToString();
    }

    public static string BundleSource(WindowsInstallerRecipe recipe, string msiPath)
    {
        Validate(recipe);
        var app = new XElement(Bal + "WixStandardBootstrapperApplication", new XAttribute("Theme", "hyperlinkLicense"),
            new XAttribute("LicenseUrl", recipe.LicenseUrl ?? ""), new XAttribute("ShowVersion", "yes"), new XAttribute("SuppressOptionsUI", "yes"));
        if (recipe.LogoFile is not null) app.Add(new XAttribute("LogoFile", recipe.LogoFile));
        if (recipe.ThemeFile is not null) app.Add(new XAttribute("ThemeFile", recipe.ThemeFile));
        var bundle = new XElement(Wix + "Bundle", new XAttribute("Name", recipe.Product + " Setup"), new XAttribute("Manufacturer", recipe.Publisher),
            new XAttribute("Version", recipe.Version), new XAttribute("UpgradeCode", recipe.BundleUpgradeCode),
            new XElement(Wix + "BootstrapperApplication", app));
        if (recipe.IconFile is not null) bundle.Add(new XAttribute("IconSourceFile", recipe.IconFile));
        if (recipe.SupportUrl is not null) bundle.Add(new XAttribute("HelpUrl", recipe.SupportUrl));
        var msi = new XElement(Wix + "MsiPackage", new XAttribute("SourceFile", Path.GetFullPath(msiPath)),
            new XAttribute("Compressed", recipe.DownloadUrl is null ? "yes" : "no"), new XAttribute("Vital", "yes"));
        if (recipe.DownloadUrl is not null) msi.Add(new XAttribute("DownloadUrl", recipe.DownloadUrl));
        bundle.Add(new XElement(Wix + "Chain", msi));
        return new XDocument(new XElement(Wix + "Wix", new XAttribute(XNamespace.Xmlns + "bal", Bal), bundle)).ToString();
    }

    public static async ValueTask BuildAsync(WindowsInstallerRecipe recipe, string wixExecutable = "wix", CancellationToken token = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Build Windows installers on a Windows runner.");
        Validate(recipe);
        if (recipe.RequireSigning && recipe.Signing is null) throw new InvalidDataException("This recipe requires a Windows signing certificate.");
        var output = Path.GetFullPath(recipe.OutputDirectory);
        Directory.CreateDirectory(output);
        var msi = Path.Combine(output, "product.msi");
        var bundle = Path.Combine(output, recipe.Product + "-setup.exe");
        var source = Path.Combine(output, "package.wxs");
        await File.WriteAllTextAsync(source, MsiSource(recipe), token);
        await RunAsync(wixExecutable, ["build", source, "-arch", recipe.Architecture, "-o", msi], token);
        if (recipe.Signing is not null) await SignAsync(msi, recipe.Signing, token);
        source = Path.Combine(output, "bundle.wxs");
        await File.WriteAllTextAsync(source, BundleSource(recipe, msi), token);
        await RunAsync(wixExecutable, ["build", source, "-arch", recipe.Architecture, "-ext", "WixToolset.Bal.wixext", "-o", bundle], token);
        if (recipe.Signing is not null)
        {
            var engine = Path.Combine(output, "burn-engine.exe");
            var signed = Path.Combine(output, "setup-signed.exe");
            await RunAsync(wixExecutable, ["burn", "detach", bundle, "-engine", engine], token);
            await SignAsync(engine, recipe.Signing, token);
            await RunAsync(wixExecutable, ["burn", "reattach", bundle, "-engine", engine, "-o", signed], token);
            await SignAsync(signed, recipe.Signing, token);
            File.Move(signed, bundle, true);
        }
    }

    public static async ValueTask SignAsync(string path, WindowsSigningOptions options, CancellationToken token = default)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Authenticode signing requires Windows.");
        if (string.IsNullOrWhiteSpace(options.CertificateThumbprint) || options.CertificateThumbprint.Any(c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Supply the signing certificate's hexadecimal thumbprint.");
        if (options.TimestampUrl.Scheme != "https" && options.TimestampUrl.Scheme != "http") throw new ArgumentException("Timestamp URL must use HTTP(S).");
        var args = new List<string> { "sign", "/sha1", options.CertificateThumbprint, "/s", options.Store ?? "My", "/fd", "SHA256", "/tr", options.TimestampUrl.AbsoluteUri, "/td", "SHA256" };
        if (options.MachineStore) args.Add("/sm");
        args.Add(Path.GetFullPath(path));
        await RunAsync(options.SignTool ?? "signtool", args, token);
        await RunAsync(options.SignTool ?? "signtool", ["verify", "/pa", "/all", "/tw", Path.GetFullPath(path)], token);
    }

    internal static async ValueTask RunAsync(string executable, IEnumerable<string> arguments, CancellationToken token)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in arguments) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {executable}.");
        var output = process.StandardOutput.ReadToEndAsync(token);
        var errors = process.StandardError.ReadToEndAsync(token);
        try { await process.WaitForExitAsync(token); }
        catch { if (!process.HasExited) process.Kill(true); throw; }
        var details = (await output) + (await errors);
        if (process.ExitCode != 0) throw new InvalidOperationException($"{Path.GetFileName(executable)} failed with exit code {process.ExitCode}: {details}");
    }

    private static string Id(string path) => "F" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..24];

    private static void Validate(WindowsInstallerRecipe recipe)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipe.Product); ArgumentException.ThrowIfNullOrWhiteSpace(recipe.Publisher);
        if (recipe.Product.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || recipe.Product.Contains('/') || recipe.Product.Contains('\\')) throw new ArgumentException("Product must be a valid directory name.");
        if (recipe.Architecture is not ("x86" or "x64" or "arm64")) throw new ArgumentException("Architecture must be x86, x64 or arm64.");
        if (recipe.Scope is not ("user" or "machine")) throw new ArgumentException("Scope must be user or machine.");
        if (recipe.UpgradeCode == Guid.Empty || recipe.BundleUpgradeCode == Guid.Empty || recipe.UpgradeCode == recipe.BundleUpgradeCode)
            throw new ArgumentException("Supply distinct, stable MSI and bundle upgrade GUIDs.");
        if (!Version.TryParse(recipe.Version, out var version) || version.Major > 255 || version.Minor > 255 || version.Build < 0 || version.Build > 65535 || version.Revision > 0)
            throw new ArgumentException("MSI version must be major.minor.build (major/minor <= 255, build <= 65535).");
        if (!Directory.Exists(recipe.SourceDirectory)) throw new DirectoryNotFoundException(recipe.SourceDirectory);
        if (Path.GetFileName(recipe.Executable) != recipe.Executable || !File.Exists(Path.Combine(recipe.SourceDirectory, recipe.Executable)))
            throw new ArgumentException("Executable must identify a file at the source directory root.");
        var source = Path.GetFullPath(recipe.SourceDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var output = Path.GetFullPath(recipe.OutputDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (output.StartsWith(source, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Installer output must be outside the payload directory.");
    }
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(WindowsInstallerRecipe))]
internal partial class WindowsInstallerJsonContext : JsonSerializerContext;
