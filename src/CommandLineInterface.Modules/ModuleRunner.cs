using System.Runtime.InteropServices;

namespace CoreVar.CommandLineInterface.Modules;

public sealed class ModuleRunner
{
    public ValueTask<int> RunAsync(ModuleManifest manifest, IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var entrypoint = SelectEntrypoint(manifest);
        var executable = ResolveExecutable(manifest, entrypoint);
        IEnumerable<string> allArguments = entrypoint.Arguments;
        if (!string.IsNullOrWhiteSpace(entrypoint.Interpreter) || manifest.Runtime.Kind is ModuleRuntimeKind.Python or ModuleRuntimeKind.Node or ModuleRuntimeKind.DotNet)
            allArguments = allArguments.Prepend(ModuleRuntimeProvisioner.SafeChild(manifest.InstallDirectory, entrypoint.Path));
        allArguments = allArguments.Concat(arguments);
        var environment = new Dictionary<string, string?>
        {
            ["COREVAR_MODULE_PROTOCOL"] = ModuleManifest.ProcessProtocol,
            ["COREVAR_MODULE_ID"] = manifest.Id,
            ["COREVAR_MODULE_VERSION"] = manifest.Version,
            ["COREVAR_CLI_VERSION"] = typeof(ModuleRunner).Assembly.GetName().Version?.ToString()
        };
        return ProcessRunner.RunAsync(executable, allArguments, manifest.InstallDirectory, environment, cancellationToken);
    }

    private static ModuleEntrypoint SelectEntrypoint(ModuleManifest manifest)
    {
        var key = $"{(OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux")}-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}";
        if (manifest.Entrypoints.TryGetValue(key, out var entrypoint)) return entrypoint;
        if (manifest.Entrypoints.TryGetValue("any", out entrypoint)) return entrypoint;
        throw new PlatformNotSupportedException($"Module '{manifest.Id}' has no entrypoint for {key}.");
    }

    private static string ResolveExecutable(ModuleManifest manifest, ModuleEntrypoint entrypoint)
    {
        if (!string.IsNullOrWhiteSpace(entrypoint.Interpreter))
            return ModuleRuntimeProvisioner.SafeChild(manifest.InstallDirectory, entrypoint.Interpreter);
        return manifest.Runtime.Kind switch
        {
            ModuleRuntimeKind.Python => ResolvePython(manifest),
            ModuleRuntimeKind.Node => ResolveNode(manifest),
            ModuleRuntimeKind.DotNet => "dotnet",
            _ => ModuleRuntimeProvisioner.SafeChild(manifest.InstallDirectory, entrypoint.Path)
        };
    }

    private static string ResolvePython(ModuleManifest manifest)
    {
        var venv = Path.Combine(manifest.InstallDirectory, ".runtime", "python");
        return OperatingSystem.IsWindows() ? Path.Combine(venv, "Scripts", "python.exe") : Path.Combine(venv, "bin", "python");
    }

    private static string ResolveNode(ModuleManifest manifest) =>
        ModuleRuntimeProvisioner.ResolveRuntimeExecutable(manifest, OperatingSystem.IsWindows() ? "node.exe" : "node", "node");
}
