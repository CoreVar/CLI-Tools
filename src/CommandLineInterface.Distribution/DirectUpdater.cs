using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.Json;
using CoreVar.CommandLineInterface.IO;

namespace CoreVar.CommandLineInterface.Distribution;

public sealed class DirectUpdater(DistributionPaths paths, ReleaseCatalogClient catalogs, ArtifactVerifier verifier, HttpClient? client = null)
{
    private readonly HttpClient _client = client ?? new HttpClient();

    public async ValueTask<InstallationState> UpdateAsync(InstallationState state, string? version = null, CancellationToken cancellationToken = default,
        Func<InstallationState, CancellationToken, ValueTask<bool>>? readiness = null)
    {
        using var operation = InstallationFiles.AcquireLock(paths.Root);
        await EnsureCurrentAsync(state, cancellationToken);
        if (state.Provider != InstallationProvider.Direct)
            throw new InvalidOperationException($"Updates for '{state.Provider}' must be delegated to its native package manager.");
        var catalog = await catalogs.LoadAsync(state.Catalog, cancellationToken);
        var release = ReleaseCatalogClient.Resolve(catalog, state.Channel, version, state.InstallationId);
        if (release.Bundle is not null && readiness is null) throw new InvalidOperationException("A bundled release requires a readiness callback before activation.");
        if (!catalog.Product.Equals(state.Product, StringComparison.Ordinal)) throw new InvalidDataException("Catalog product does not match the installed product.");
        if (release.Version.Equals(state.Version, StringComparison.OrdinalIgnoreCase))
        {
            if (readiness is not null && !await readiness(state, cancellationToken)) throw new InvalidOperationException("Release readiness failed.");
            return state;
        }
        var launcherText = Environment.GetEnvironmentVariable("COREVAR_CLI_LAUNCHER_VERSION") ?? state.LauncherVersion;
        if (Version.TryParse(release.MinimumLauncherVersion, out var minimumLauncher) &&
            (!Version.TryParse(launcherText, out var launcherVersion) || launcherVersion.CompareTo(minimumLauncher) < 0))
            throw new InvalidOperationException($"Release '{release.Version}' requires launcher {minimumLauncher} or newer. Re-run the bootstrap installer to upgrade the launcher safely.");
        var rid = $"{(OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux")}-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}";
        var artifact = release.Artifacts.FirstOrDefault(item => item.RuntimeIdentifier.Equals(rid, StringComparison.OrdinalIgnoreCase))
            ?? throw new PlatformNotSupportedException($"Release '{release.Version}' has no artifact for {rid}.");
        if (catalog.Revocations.Any(item => item.Sha256?.Equals(artifact.Sha256, StringComparison.OrdinalIgnoreCase) == true))
            throw new InvalidOperationException("The selected release artifact has been revoked.");

        Directory.CreateDirectory(paths.Cache);
        _ = paths.Version(release.Version);
        var archive = Path.Combine(paths.Cache, $"{release.Version}-{Guid.NewGuid():N}.zip");
        try
        {
            await DownloadAsync(artifact.Uri, archive, cancellationToken);
            await verifier.VerifyAsync(archive, artifact, cancellationToken);
            var target = paths.Version(release.Version);
            var staging = target + ".staging-" + Guid.NewGuid().ToString("N");
            try
            {
                InstallationFiles.Extract(archive, staging, cancellationToken);
                var marker = Path.Combine(target, ".corevar-artifact.sha256");
                if (Directory.Exists(target))
                {
                    if (!File.Exists(marker) || !File.ReadAllText(marker).Equals(artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("An existing version directory has different or unknown contents. It will not be overwritten.");
                }
                else
                {
                File.WriteAllText(Path.Combine(staging, ".corevar-artifact.sha256"), artifact.Sha256);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                Directory.Move(staging, target);
                }
            }
            finally { if (Directory.Exists(staging)) Directory.Delete(staging, true); }
            var updated = new InstallationState
            {
                Product = state.Product, Version = release.Version, PreviousVersion = state.Version,
                Channel = state.Channel, Catalog = state.Catalog, Provider = state.Provider,
                PackageId = state.PackageId, Entrypoint = state.Entrypoint, InstallationId = state.InstallationId, LauncherVersion = launcherText
            };
            // Readiness executes the candidate directly while the old state remains active.
            if (readiness is not null && !await readiness(updated, cancellationToken))
                throw new InvalidOperationException($"Release '{updated.Version}' failed readiness validation. The previous host remains active.");
            await WriteStateAsync(updated, cancellationToken);
            return updated;
        }
        finally { if (File.Exists(archive)) File.Delete(archive); }
    }

    public async ValueTask<InstallationState> RollbackAsync(InstallationState state, CancellationToken cancellationToken = default)
    {
        using var operation = InstallationFiles.AcquireLock(paths.Root);
        await EnsureCurrentAsync(state, cancellationToken);
        if (state.Provider != InstallationProvider.Direct) throw new InvalidOperationException("Rollback must be delegated to the installation's package manager.");
        if (state.PreviousVersion is null || !Directory.Exists(paths.Version(state.PreviousVersion)))
            throw new InvalidOperationException("No previous installed version is available.");
        var rolledBack = new InstallationState
        {
            Product = state.Product, Version = state.PreviousVersion, PreviousVersion = state.Version,
            Channel = state.Channel, Catalog = state.Catalog, Provider = state.Provider,
            PackageId = state.PackageId, Entrypoint = state.Entrypoint, InstallationId = state.InstallationId, LauncherVersion = state.LauncherVersion
        };
        await WriteStateAsync(rolledBack, cancellationToken);
        return rolledBack;
    }

    private async ValueTask WriteStateAsync(InstallationState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(paths.Root);
        var temporary = paths.State + ".new-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = File.Create(temporary))
            {
                await JsonSerializer.SerializeAsync(stream, state, DistributionJsonContext.Default.InstallationState, cancellationToken);
                stream.Flush(true);
            }
            File.Move(temporary, paths.State, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private async ValueTask DownloadAsync(Uri uri, string target, CancellationToken cancellationToken)
    {
        if (uri.IsFile) { File.Copy(uri.LocalPath, target, true); return; }
        await using var input = await _client.GetStreamAsync(uri, cancellationToken);
        await using var output = File.Create(target);
        await input.CopyToAsync(output, cancellationToken);
    }

    private async ValueTask EnsureCurrentAsync(InstallationState expected, CancellationToken token)
    {
        if (!File.Exists(paths.State)) return;
        await using var stream = File.OpenRead(paths.State);
        var current = await JsonSerializer.DeserializeAsync(stream, DistributionJsonContext.Default.InstallationState, token);
        if (current is null || current.Version != expected.Version || current.InstallationId != expected.InstallationId)
            throw new InvalidOperationException("Installation state changed. Reload it before retrying the operation.");
    }
}
