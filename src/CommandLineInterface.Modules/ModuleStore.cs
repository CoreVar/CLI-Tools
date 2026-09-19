using System.Text.Json;

namespace CoreVar.CommandLineInterface.Modules;

public sealed class ModuleStore(ModulePaths paths)
{
    public IEnumerable<string> InstalledModuleIds() => Directory.Exists(paths.Modules)
        ? Directory.EnumerateDirectories(paths.Modules).Select(path => Path.GetFileName(path)!)
        : [];

    public async ValueTask<ModuleInstallationPointer?> GetPointerAsync(string id, CancellationToken cancellationToken = default)
    {
        var path = paths.Current(id);
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync(stream, ModuleJsonContext.Default.ModuleInstallationPointer, cancellationToken);
    }

    public async ValueTask<ModuleManifest?> GetCurrentAsync(string id, CancellationToken cancellationToken = default)
    {
        var pointer = await GetPointerAsync(id, cancellationToken);
        if (pointer is null) return null;
        var directory = paths.Version(id, pointer.Version);
        var manifestPath = Path.Combine(directory, "corevar.module.json");
        if (!File.Exists(manifestPath)) return null;
        await using var stream = File.OpenRead(manifestPath);
        var manifest = await JsonSerializer.DeserializeAsync(stream, ModuleJsonContext.Default.ModuleManifest, cancellationToken);
        if (manifest is not null) manifest.InstallDirectory = directory;
        return manifest;
    }

    internal async ValueTask ActivateAsync(string id, ModuleInstallationPointer pointer, CancellationToken cancellationToken)
    {
        var moduleDirectory = paths.Module(id);
        Directory.CreateDirectory(moduleDirectory);
        var target = paths.Current(id);
        var temporary = target + ".new-" + Guid.NewGuid().ToString("N");
        await using (var stream = File.Create(temporary))
            await JsonSerializer.SerializeAsync(stream, pointer, ModuleJsonContext.Default.ModuleInstallationPointer, cancellationToken);
        File.Move(temporary, target, true);
    }
}
