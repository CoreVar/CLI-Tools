namespace CoreVar.CommandLineInterface.Modules;

public sealed class ModuleRuntimeProvisioner
{
    public async ValueTask ProvisionAsync(ModuleManifest manifest, CancellationToken cancellationToken = default)
    {
        switch (manifest.Runtime.Kind)
        {
            case ModuleRuntimeKind.Python:
                await ProvisionPythonAsync(manifest, cancellationToken);
                break;
            case ModuleRuntimeKind.Node:
                await ProvisionNodeAsync(manifest, cancellationToken);
                break;
        }
    }

    private static async ValueTask ProvisionPythonAsync(ModuleManifest manifest, CancellationToken cancellationToken)
    {
        var runtimeDirectory = Path.Combine(manifest.InstallDirectory, ".runtime", "python");
        var sourcePython = ResolveRuntimeExecutable(manifest, OperatingSystem.IsWindows() ? "python.exe" : "python", "python");
        var venvPython = OperatingSystem.IsWindows()
            ? Path.Combine(runtimeDirectory, "Scripts", "python.exe")
            : Path.Combine(runtimeDirectory, "bin", "python");
        if (!File.Exists(venvPython))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(runtimeDirectory)!);
            EnsureSuccess(await ProcessRunner.RunAsync(sourcePython, ["-m", "venv", runtimeDirectory], manifest.InstallDirectory, null, cancellationToken), "create the Python virtual environment");
        }
        if (!string.IsNullOrWhiteSpace(manifest.Runtime.RequirementsFile))
        {
            var requirements = SafeChild(manifest.InstallDirectory, manifest.Runtime.RequirementsFile);
            EnsureSuccess(await ProcessRunner.RunAsync(venvPython, ["-m", "pip", "install", "--disable-pip-version-check", "--requirement", requirements], manifest.InstallDirectory, null, cancellationToken), "restore Python dependencies");
        }
    }

    private static async ValueTask ProvisionNodeAsync(ModuleManifest manifest, CancellationToken cancellationToken)
    {
        var packageDirectory = SafeChild(manifest.InstallDirectory, manifest.Runtime.PackageDirectory ?? ".");
        var npm = ResolveRuntimeExecutable(manifest, OperatingSystem.IsWindows() ? "npm.cmd" : "npm", "npm");
        if (!File.Exists(Path.Combine(packageDirectory, "package-lock.json")))
            throw new InvalidDataException("Node modules must include package-lock.json for reproducible installation.");
        EnsureSuccess(await ProcessRunner.RunAsync(npm, ["ci", "--omit=dev", "--ignore-scripts", "--no-audit", "--no-fund"], packageDirectory, null, cancellationToken), "restore Node dependencies");
    }

    internal static string ResolveRuntimeExecutable(ModuleManifest manifest, string bundledDefault, string systemDefault)
    {
        if (!string.IsNullOrWhiteSpace(manifest.Runtime.Executable))
            return manifest.Runtime.Provisioning == ModuleRuntimeProvisioning.Bundled
                ? SafeChild(manifest.InstallDirectory, manifest.Runtime.Executable)
                : manifest.Runtime.Executable;
        return manifest.Runtime.Provisioning == ModuleRuntimeProvisioning.Bundled
            ? SafeChild(manifest.InstallDirectory, Path.Combine("runtime", bundledDefault))
            : systemDefault;
    }

    internal static string SafeChild(string root, string relative)
    {
        var fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!full.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) && !string.Equals(full.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A module path escaped its installation directory.");
        return full;
    }

    private static void EnsureSuccess(int exitCode, string operation)
    {
        if (exitCode != 0) throw new InvalidOperationException($"Failed to {operation}; process exited with code {exitCode}.");
    }
}
