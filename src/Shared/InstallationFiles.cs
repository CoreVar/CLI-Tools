using System.IO.Compression;

namespace CoreVar.CommandLineInterface.IO;

internal static class InstallationFiles
{
    internal static IEnumerable<string> PayloadFiles(string root)
    {
        foreach (var entry in new DirectoryInfo(root).EnumerateFileSystemInfos())
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Payload links are not supported.");
            if (entry is DirectoryInfo directory)
                foreach (var file in PayloadFiles(directory.FullName)) yield return file;
            else yield return entry.FullName;
        }
    }

    internal static FileStream AcquireLock(string root)
    {
        Directory.CreateDirectory(root);
        try { return new FileStream(Path.Combine(root, ".operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException exception) { throw new IOException("Another installation operation is running. Try again when it finishes.", exception); }
        // Keep the lock file to avoid locking different inodes on Unix.
    }

    internal static void Extract(string archivePath, string destination, CancellationToken token = default)
    {
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        using var archive = ZipFile.OpenRead(archivePath);
        long total = 0;
        var names = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            token.ThrowIfCancellationRequested();
            var normalized = entry.FullName.Replace('\\', '/');
            if (normalized.StartsWith('/') || normalized.Split('/').Any(part => part == ".." || part.Contains(':')))
                throw new InvalidDataException("Archive path traversal was blocked.");
            var target = Path.GetFullPath(Path.Combine(destination, normalized));
            if (!target.StartsWith(root, comparison) || !names.Add(target))
                throw new InvalidDataException("Archive contains an unsafe or duplicate path.");
            var kind = (entry.ExternalAttributes >> 16) & 0xF000;
            if (kind != 0 && kind != 0x8000 && kind != 0x4000)
                throw new InvalidDataException("Archive links and special files are not supported.");
            total = checked(total + entry.Length);
            if (total > 4L * 1024 * 1024 * 1024 || archive.Entries.Count > 100000)
                throw new InvalidDataException("Archive exceeds the 4 GiB or 100,000 entry extraction limit.");
            if (normalized.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, false);
            if (!OperatingSystem.IsWindows())
            {
                var permissions = (entry.ExternalAttributes >> 16) & 0x1FF;
                if (permissions != 0) File.SetUnixFileMode(target, (UnixFileMode)permissions);
            }
        }
    }
}
