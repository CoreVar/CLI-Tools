using System.Text.Json;

namespace CoreVar.CommandLineInterface.Distribution;

public sealed class ReleaseCatalogClient(HttpClient? client = null)
{
    private readonly HttpClient _client = client ?? new HttpClient();

    public async ValueTask<ReleaseCatalog> LoadAsync(Uri uri, CancellationToken cancellationToken = default)
    {
        if (uri.IsFile)
        {
            await using var file = File.OpenRead(uri.LocalPath);
            return await JsonSerializer.DeserializeAsync(file, DistributionJsonContext.Default.ReleaseCatalog, cancellationToken)
                ?? throw new InvalidDataException("The release catalog is empty.");
        }
        await using var stream = await _client.GetStreamAsync(uri, cancellationToken);
        return await JsonSerializer.DeserializeAsync(stream, DistributionJsonContext.Default.ReleaseCatalog, cancellationToken)
            ?? throw new InvalidDataException("The release catalog is empty.");
    }

    public static ReleaseManifest Resolve(ReleaseCatalog catalog, string channel, string? version = null, string? installationId = null)
    {
        version ??= catalog.Channels.TryGetValue(channel, out var selected) ? selected : null;
        if (version is null) throw new KeyNotFoundException($"Channel '{channel}' does not exist.");
        var rollout = catalog.Rollouts.LastOrDefault(item => item.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase) && item.Version.Equals(version, StringComparison.OrdinalIgnoreCase));
        if (rollout is not null && rollout.Percentage < 100 && rollout.FallbackVersion is not null && !Included(installationId ?? "anonymous", rollout))
            version = rollout.FallbackVersion;
        var release = catalog.Releases.FirstOrDefault(item => item.Version.Equals(version, StringComparison.OrdinalIgnoreCase))
            ?? throw new KeyNotFoundException($"Release '{version}' does not exist.");
        var revoked = catalog.Revocations.FirstOrDefault(item => item.Version?.Equals(version, StringComparison.OrdinalIgnoreCase) == true);
        if (revoked is not null) throw new InvalidOperationException($"Release '{version}' is revoked: {revoked.Reason}");
        return release;
    }

    private static bool Included(string installationId, ChannelRollout rollout)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{rollout.Seed}:{installationId}"));
        var bucket = BitConverter.ToUInt32(bytes, 0) % 100;
        return bucket < Math.Clamp(rollout.Percentage, 0, 100);
    }
}
