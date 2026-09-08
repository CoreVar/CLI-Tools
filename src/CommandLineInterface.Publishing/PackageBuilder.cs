using System.IO.Compression;

namespace CoreVar.CommandLineInterface.Publishing;

public static class PackageBuilder
{
    /// <summary>Creates a reproducible ZIP package from a CLI or module publish directory.</summary>
    public static void Create(string sourceDirectory, string outputPath, string? launcherPath = null)
    {
        var source = Path.GetFullPath(sourceDirectory);
        if (!Directory.Exists(source)) throw new DirectoryNotFoundException(source);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        using var output = File.Create(outputPath);
        using var archive = new ZipArchive(output, ZipArchiveMode.Create);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).OrderBy(value => value, StringComparer.Ordinal))
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
