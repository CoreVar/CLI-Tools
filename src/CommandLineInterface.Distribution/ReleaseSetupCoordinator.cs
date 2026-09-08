namespace CoreVar.CommandLineInterface.Distribution;

/// <summary>Updates a direct installation, validates product readiness, and restores the previous host on failure.</summary>
public sealed class ReleaseSetupCoordinator(DirectUpdater updater)
{
    public async ValueTask<InstallationState> UpdateAndVerifyAsync(InstallationState current,
        Func<InstallationState, CancellationToken, ValueTask<bool>> readiness, string? version = null,
        CancellationToken cancellationToken = default)
    {
        var updated = await updater.UpdateAsync(current, version, cancellationToken);
        if (ReferenceEquals(updated, current) || await readiness(updated, cancellationToken)) return updated;
        try { await updater.RollbackAsync(updated, cancellationToken); }
        catch (Exception rollback) { throw new AggregateException("Updated release failed readiness and host rollback also failed.", rollback); }
        throw new InvalidOperationException($"Release '{updated.Version}' failed readiness validation; previous host '{current.Version}' was restored.");
    }
}
