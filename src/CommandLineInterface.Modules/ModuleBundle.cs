using CoreVar.CommandLineInterface.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace CoreVar.CommandLineInterface.Modules;

/// <summary>An immutable, reproducible collection of module releases.</summary>
public sealed class ModuleBundle
{
    public const string CurrentSchema = "corevar.cli.bundle/1";
    public string Schema { get; init; } = CurrentSchema;
    public required string Id { get; init; }
    public required string Snapshot { get; init; }
    public string? HostCompatibility { get; init; }
    public string? FrameworkCompatibility { get; init; }
    public Uri? Catalog { get; init; }
    public List<ModuleBundleMember> Modules { get; init; } = [];
}

public sealed class ModuleBundleMember
{
    public required string Id { get; init; }
    public required string Version { get; init; }
    public required string Sha256 { get; init; }
    public string Channel { get; init; } = "stable";
    public bool Required { get; init; } = true;
    public Uri? Catalog { get; init; }
    public List<string> Platforms { get; init; } = [];
    public List<string> Architectures { get; init; } = [];
    public List<string> RuntimeIdentifiers { get; init; } = [];
}

public sealed class ModuleInstallContext
{
    public required Version HostVersion { get; init; }
    public Version FrameworkVersion { get; init; } = typeof(ModuleBundle).Assembly.GetName().Version ?? new(1, 0);
    public required string Root { get; init; }
    public string RuntimeIdentifier { get; init; } = ModuleCompatibility.CurrentRuntimeIdentifier();
}

public sealed class ModuleBundleState
{
    public required string BundleId { get; init; }
    public required string Snapshot { get; init; }
    public required string Sha256 { get; init; }
    public DateTimeOffset InstalledAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum ModuleBundleItemStatus { Installed, Skipped, Incompatible, Failed, RolledBack }

public sealed record ModuleBundleItemResult(string Id, string Version, bool Required,
    ModuleBundleItemStatus Status, string? Message = null);

public sealed class ModuleBundleInstallResult
{
    public required string BundleId { get; init; }
    public required string Snapshot { get; init; }
    public List<ModuleBundleItemResult> Items { get; init; } = [];
    public bool IsComplete => Items.All(item => !item.Required || item.Status == ModuleBundleItemStatus.Installed);
}

public sealed class ModuleBundleClient(HttpClient? client = null)
{
    private readonly HttpClient _client = client ?? new HttpClient();

    public async ValueTask<(ModuleBundle Bundle, Uri BaseUri)> LoadAsync(string pathOrUri, string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        var source = Uri.TryCreate(pathOrUri, UriKind.Absolute, out var absolute) ? absolute : new Uri(Path.GetFullPath(pathOrUri));
        byte[] content = source.IsFile
            ? await File.ReadAllBytesAsync(source.LocalPath, cancellationToken)
            : await _client.GetByteArrayAsync(source, cancellationToken);
        var actual = Convert.ToHexString(SHA256.HashData(content));
        if (!actual.Equals(Normalize(expectedSha256), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The module bundle SHA-256 digest did not match release metadata.");
        var bundle = JsonSerializer.Deserialize(content, ModuleJsonContext.Default.ModuleBundle)
            ?? throw new InvalidDataException("The module bundle is empty.");
        if (bundle.Schema != ModuleBundle.CurrentSchema) throw new InvalidDataException($"Unsupported bundle schema '{bundle.Schema}'.");
        if (bundle.Modules is null || bundle.Modules.Count == 0) throw new InvalidDataException("A module bundle must contain at least one module.");
        var baseUri = source.IsFile ? new Uri(Path.GetDirectoryName(source.LocalPath)! + Path.DirectorySeparatorChar) : new Uri(source, ".");
        return (bundle, baseUri);
    }

    internal static string Normalize(string value) => value.Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("-", string.Empty);
}

public sealed class ModuleBundleInstaller(HttpClient? client = null)
{
    private readonly HttpClient _client = client ?? new HttpClient();

    public async ValueTask<ModuleBundleInstallResult> InstallAsync(string manifestPathOrUri, string manifestSha256,
        ModuleInstallContext context, CancellationToken cancellationToken = default)
    {
        var (bundle, baseUri) = await new ModuleBundleClient(_client).LoadAsync(manifestPathOrUri, manifestSha256, cancellationToken);
        if (!ModuleCompatibility.IsCompatible(bundle.HostCompatibility, context.HostVersion))
            throw new InvalidDataException($"Bundle '{bundle.Id}' is not compatible with host {context.HostVersion}.");
        if (!ModuleCompatibility.IsCompatible(bundle.FrameworkCompatibility, context.FrameworkVersion))
            throw new InvalidDataException($"Bundle '{bundle.Id}' is not compatible with framework {context.FrameworkVersion}.");

        var paths = new ModulePaths(context.Root);
        var store = new ModuleStore(paths);
        var installer = new ModuleInstaller(paths, store, new ModuleRuntimeProvisioner(), _client, context.HostVersion);
        var catalogs = new ModuleCatalogClient(_client);
        var result = new ModuleBundleInstallResult { BundleId = bundle.Id, Snapshot = bundle.Snapshot };
        var changed = new List<(string Id, byte[]? Previous)>();
        var locks = new List<FileStream>();
        if (bundle.Modules.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != bundle.Modules.Count)
            throw new InvalidDataException("A bundle cannot contain duplicate module IDs.");
        try
        {
            // The host installer already holds the root operation lock while invoking setup.
            // Serialize bundle commits separately so that post-install setup can run beneath it.
            locks.Add(InstallationFiles.AcquireLock(Path.Combine(paths.Root, ".bundle-transaction")));
            foreach (var member in bundle.Modules.OrderBy(x => x.Id, StringComparer.OrdinalIgnoreCase))
                locks.Add(InstallationFiles.AcquireLock(paths.Module(member.Id)));
            try
            {

                foreach (var member in bundle.Modules)
                {
                    if (!ModuleCompatibility.IsCompatible(member, context.RuntimeIdentifier, out var reason))
                    {
                        result.Items.Add(new(member.Id, member.Version, member.Required, ModuleBundleItemStatus.Incompatible, reason));
                        if (member.Required) { await RollbackAsync(changed, paths, result); return result; }
                        continue;
                    }
                    try
                    {
                        var catalogUri = Resolve(member.Catalog ?? bundle.Catalog
                            ?? throw new InvalidDataException($"Module '{member.Id}' has no catalog."), baseUri);
                        var catalog = await catalogs.LoadAsync(catalogUri, cancellationToken);
                        var release = ModuleCatalogClient.SelectRelease(catalog, member.Id,
                            string.IsNullOrWhiteSpace(member.Channel) ? "stable" : member.Channel, member.Version);
                        if (!ModuleBundleClient.Normalize(release.Sha256).Equals(ModuleBundleClient.Normalize(member.Sha256), StringComparison.OrdinalIgnoreCase))
                            throw new InvalidDataException($"Module '{member.Id}' catalog digest does not match the pinned bundle snapshot.");
                        var pointerPath = paths.Current(member.Id);
                        var previous = File.Exists(pointerPath) ? await File.ReadAllBytesAsync(pointerPath, cancellationToken) : null;
                        await installer.InstallUnderLockAsync(member.Id, release, catalogUri, cancellationToken);
                        changed.Add((member.Id, previous));
                        result.Items.Add(new(member.Id, member.Version, member.Required, ModuleBundleItemStatus.Installed));
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        result.Items.Add(new(member.Id, member.Version, member.Required, ModuleBundleItemStatus.Failed, exception.Message));
                        if (member.Required) { await RollbackAsync(changed, paths, result); return result; }
                    }
                }
                if (result.IsComplete)
                {
                    Directory.CreateDirectory(context.Root);
                    var state = new ModuleBundleState { BundleId = bundle.Id, Snapshot = bundle.Snapshot, Sha256 = ModuleBundleClient.Normalize(manifestSha256) };
                    var statePath = Path.Combine(context.Root, "bundle-state.json");
                    var temporary = statePath + ".new-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        await File.WriteAllTextAsync(temporary,
                            JsonSerializer.Serialize(state, ModuleJsonContext.Default.ModuleBundleState), cancellationToken);
                        File.Move(temporary, statePath, true);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                }
                return result;
            }
            catch
            {
                // A cancelled request must not cancel recovery of already activated members.
                await RollbackAsync(changed, paths, result);
                throw;
            }
        }
        finally { foreach (var operation in locks.AsEnumerable().Reverse()) operation.Dispose(); }
    }

    private static Uri Resolve(Uri value, Uri baseUri) => value.IsAbsoluteUri ? value : new Uri(baseUri, value);

    private static async ValueTask RollbackAsync(List<(string Id, byte[]? Previous)> changed,
        ModulePaths paths, ModuleBundleInstallResult result)
    {
        foreach (var item in changed.AsEnumerable().Reverse())
        {
            var target = paths.Current(item.Id);
            if (item.Previous is null) File.Delete(target);
            else
            {
                var temporary = target + ".restore-" + Guid.NewGuid().ToString("N");
                try
                {
                    await File.WriteAllBytesAsync(temporary, item.Previous, CancellationToken.None);
                    File.Move(temporary, target, true);
                }
                finally { if (File.Exists(temporary)) File.Delete(temporary); }
            }
            var index = result.Items.FindLastIndex(candidate => candidate.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase)
                && candidate.Status == ModuleBundleItemStatus.Installed);
            if (index >= 0) result.Items[index] = result.Items[index] with { Status = ModuleBundleItemStatus.RolledBack };
        }
    }
}
