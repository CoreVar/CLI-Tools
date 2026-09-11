using CoreVar.CommandLineInterface.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace CoreVar.CommandLineInterface.Modules;

public sealed class ModuleInstaller(ModulePaths paths, ModuleStore store, ModuleRuntimeProvisioner provisioner,
    HttpClient? httpClient = null, Version? hostVersion = null)
{
    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();
    private readonly Version _hostVersion = hostVersion ?? typeof(ModuleInstaller).Assembly.GetName().Version ?? new Version(1, 0);

    public async ValueTask<ModuleManifest> InstallAsync(string expectedId, ModuleRelease release,
        Uri? catalog = null, CancellationToken cancellationToken = default)
    {
        ModulePaths.ValidateSegment(expectedId); ModulePaths.ValidateSegment(release.Version);
        using var operation = InstallationFiles.AcquireLock(paths.Module(expectedId));
        if (release.Revoked) throw new InvalidOperationException("A revoked module release cannot be installed.");
        Directory.CreateDirectory(paths.Cache);
        var packagePath = Path.Combine(paths.Cache, $"{expectedId}-{release.Version}-{Guid.NewGuid():N}.zip");
        try
        {
            await DownloadAsync(release.Package, packagePath, cancellationToken);
            await VerifyHashAsync(packagePath, release.Sha256, cancellationToken);
            var staging = paths.Version(expectedId, release.Version) + ".staging-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(staging);
            try
            {
                InstallationFiles.Extract(packagePath, staging, cancellationToken);
                var manifestPath = Path.Combine(staging, "corevar.module.json");
                if (!File.Exists(manifestPath)) throw new InvalidDataException("The package does not contain corevar.module.json at its root.");
                ModuleManifest manifest;
                await using (var manifestStream = File.OpenRead(manifestPath))
                    manifest = await JsonSerializer.DeserializeAsync(manifestStream, ModuleJsonContext.Default.ModuleManifest, cancellationToken)
                        ?? throw new InvalidDataException("The module manifest is empty.");
                ValidateManifest(manifest, expectedId, release.Version);
                if (!ModuleCompatibility.IsCompatible(manifest.CliCompatibility, _hostVersion))
                    throw new InvalidDataException($"Module '{manifest.Id}' {manifest.Version} is not compatible with CLI {_hostVersion}.");
                manifest.InstallDirectory = staging;
                await provisioner.ProvisionAsync(manifest, cancellationToken);

                var target = paths.Version(expectedId, release.Version);
                if (Directory.Exists(target))
                {
                    var marker = Path.Combine(target, ".corevar-artifact.sha256");
                    if (!File.Exists(marker) || !File.ReadAllText(marker).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Existing module version has different or unknown contents and will not be overwritten.");
                }
                else
                {
                    File.WriteAllText(Path.Combine(staging, ".corevar-artifact.sha256"), release.Sha256);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    Directory.Move(staging, target);
                }
                manifest.InstallDirectory = target;
                var previous = await store.GetPointerAsync(expectedId, cancellationToken);
                await store.ActivateAsync(expectedId, new ModuleInstallationPointer
                {
                    Version = release.Version,
                    PreviousVersion = previous?.Version == release.Version ? previous.PreviousVersion : previous?.Version,
                    Channel = release.Channel,
                    Catalog = catalog
                }, cancellationToken);
                return manifest;
            }
            finally
            {
                if (Directory.Exists(staging)) Directory.Delete(staging, true);
            }
        }
        finally
        {
            if (File.Exists(packagePath)) File.Delete(packagePath);
        }
    }

    public async ValueTask<bool> RollbackAsync(string id, CancellationToken cancellationToken = default)
    {
        using var operation = InstallationFiles.AcquireLock(paths.Module(id));
        var pointer = await store.GetPointerAsync(id, cancellationToken);
        if (pointer?.PreviousVersion is null) return false;
        if (!Directory.Exists(paths.Version(id, pointer.PreviousVersion))) return false;
        await store.ActivateAsync(id, new ModuleInstallationPointer
        {
            Version = pointer.PreviousVersion,
            PreviousVersion = pointer.Version,
            Channel = pointer.Channel,
            Catalog = pointer.Catalog
        }, cancellationToken);
        return true;
    }

    public void Remove(string id)
    {
        var directory = paths.Module(id);
        if (!Directory.Exists(directory)) return;
        using var operation = InstallationFiles.AcquireLock(directory);
        // Retain the locked directory and lock file so another process cannot bypass the lock.
        foreach (var child in Directory.EnumerateDirectories(directory)) Directory.Delete(child, true);
        foreach (var file in Directory.EnumerateFiles(directory))
            if (Path.GetFileName(file) != ".operation.lock") File.Delete(file);
    }

    private async ValueTask DownloadAsync(Uri source, string target, CancellationToken cancellationToken)
    {
        if (source.IsFile)
        {
            File.Copy(source.LocalPath, target, true);
            return;
        }
        await using var sourceStream = await _httpClient.GetStreamAsync(source, cancellationToken);
        await using var targetStream = File.Create(target);
        await sourceStream.CopyToAsync(targetStream, cancellationToken);
    }

    private static async ValueTask VerifyHashAsync(string path, string expected, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        var normalized = expected.Replace("sha256:", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("-", string.Empty);
        if (!actual.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The module package SHA-256 digest did not match its catalog entry.");
    }

    private static void ValidateManifest(ModuleManifest manifest, string expectedId, string expectedVersion)
    {
        if (manifest.SchemaVersion != ModuleManifest.CurrentSchema)
            throw new InvalidDataException($"Unsupported module schema '{manifest.SchemaVersion}'.");
        if (!manifest.Id.Equals(expectedId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The module ID does not match the requested package.");
        if (!manifest.Version.Equals(expectedVersion, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The module version does not match the catalog release.");
        ModulePaths.ValidateSegment(manifest.Id);
        ModulePaths.ValidateSegment(manifest.Version);
        if (manifest.Commands.Count == 0) throw new InvalidDataException("A module must expose at least one command.");
    }
}
