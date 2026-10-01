namespace CoreVar.CommandLineInterface.Modules;

public sealed class ModuleManager(ModuleCatalogClient catalogs, ModuleInstaller installer, ModuleStore store)
{
    public async ValueTask<ModuleManifest> InstallAsync(Uri catalogUri, string id, string channel = "stable",
        string? version = null, CancellationToken cancellationToken = default)
    {
        var catalog = await catalogs.LoadAsync(catalogUri, cancellationToken);
        var release = ModuleCatalogClient.SelectRelease(catalog, id, channel, version);
        return await installer.InstallAsync(id, release, catalogUri, cancellationToken);
    }

    public async ValueTask<ModuleManifest?> UpdateAsync(string id, CancellationToken cancellationToken = default)
    {
        var pointer = await store.GetPointerAsync(id, cancellationToken);
        if (pointer?.Catalog is null) throw new InvalidOperationException($"Module '{id}' has no update catalog.");
        var catalog = await catalogs.LoadAsync(pointer.Catalog, cancellationToken);
        var release = ModuleCatalogClient.SelectRelease(catalog, id, pointer.Channel);
        if (release.Version.Equals(pointer.Version, StringComparison.OrdinalIgnoreCase)) return null;
        return await installer.InstallAsync(id, release, pointer.Catalog, cancellationToken);
    }

    public ValueTask<bool> RollbackAsync(string id, CancellationToken cancellationToken = default) => installer.RollbackAsync(id, cancellationToken);
    public void Remove(string id) => installer.Remove(id);
}
