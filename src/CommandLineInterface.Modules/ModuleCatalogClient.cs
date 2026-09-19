using System.Text.Json;

namespace CoreVar.CommandLineInterface.Modules;

public sealed class ModuleCatalogClient(HttpClient? httpClient = null)
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

    public async ValueTask<ModuleCatalog> LoadAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (uri.IsFile)
        {
            await using var file = File.OpenRead(uri.LocalPath);
            return await JsonSerializer.DeserializeAsync(file, ModuleJsonContext.Default.ModuleCatalog, cancellationToken)
                ?? throw new InvalidDataException("The module catalog is empty.");
        }
        await using var stream = await _httpClient.GetStreamAsync(uri, cancellationToken);
        return await JsonSerializer.DeserializeAsync(stream, ModuleJsonContext.Default.ModuleCatalog, cancellationToken)
            ?? throw new InvalidDataException("The module catalog is empty.");
    }

    public static ModuleRelease SelectRelease(ModuleCatalog catalog, string id, string channel = "stable", string? version = null)
    {
        var module = catalog.Modules.FirstOrDefault(item => item.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Module '{id}' was not found.");
        var candidates = module.Releases.Where(item => !item.Revoked && item.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));
        if (version is not null) candidates = candidates.Where(item => item.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
        return candidates.OrderByDescending(item => VersionKey(item.Version)).FirstOrDefault()
            ?? throw new KeyNotFoundException($"No matching release of module '{id}' was found.");
    }

    private static (Version Numeric, int Stability, string Text) VersionKey(string value)
    {
        var split = value.Split('-', 2);
        return (Version.TryParse(split[0], out var numeric) ? numeric : new Version(), split.Length == 1 ? 1 : 0, value);
    }
}
