using System.IO.Compression;
using CoreVar.CommandLineInterface.IO;

namespace CoreVar.CommandLineInterface.Publishing;

public static class PackageBuilder
{
    /// <summary>Creates a reproducible ZIP package from a CLI or module publish directory.</summary>
    public static void Create(string sourceDirectory, string outputPath, string? launcherPath = null)
    {
        var source = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        var destination = Path.GetFullPath(outputPath);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (destination.StartsWith(source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison))
            throw new ArgumentException("Package output must be outside the source directory.", nameof(outputPath));
        if (launcherPath is not null && !File.Exists(launcherPath)) throw new FileNotFoundException("Launcher was not found.", launcherPath);
        var files = InstallationFiles.PayloadFiles(source).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (launcherPath is not null && files.Any(file => Path.GetRelativePath(source, file).Replace('\\', '/').Equals(".corevar/launcher" + Path.GetExtension(launcherPath), StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The payload already contains a launcher at the reserved package path.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var output = File.Create(outputPath);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create);
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(source, file).Replace('\\', '/');
            if (relative.Split('/').Any(part => part is ".git" or "obj" or "node_modules" or ".venv")) continue;
            var entry = archive.CreateEntry(relative, CompressionLevel.SmallestSize);
            PreserveUnixPermissions(file, entry);
            entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var input = File.OpenRead(file);
            using var target = entry.Open();
            input.CopyTo(target);
        }
        if (!string.IsNullOrWhiteSpace(launcherPath))
        {
            var launcherName = Path.GetExtension(launcherPath).Equals(".exe", StringComparison.OrdinalIgnoreCase) ? "launcher.exe" : "launcher";
            var entry = archive.CreateEntry(".corevar/" + launcherName, CompressionLevel.SmallestSize);
            if (!OperatingSystem.IsWindows()) entry.ExternalAttributes = 0x81ED << 16;
            entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var input = File.OpenRead(launcherPath);
            using var target = entry.Open();
            input.CopyTo(target);
        }
    }
    private static void PreserveUnixPermissions(string file, ZipArchiveEntry entry)
    {
        if (OperatingSystem.IsWindows()) return;
        entry.ExternalAttributes = (0x8000 | ((int)File.GetUnixFileMode(file) & 0x1FF)) << 16;
    }
}
