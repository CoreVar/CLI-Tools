namespace CoreVar.CommandLineInterface.Distribution;

/// <summary>Updates a direct installation, validates product readiness, and restores the previous host on failure.</summary>
public sealed class ReleaseSetupCoordinator(DirectUpdater updater)
{
    public async ValueTask<InstallationState> UpdateAndVerifyAsync(InstallationState current,
        Func<InstallationState, CancellationToken, ValueTask<bool>> readiness, string? version = null,
        CancellationToken cancellationToken = default)
    {
        var updated = await updater.UpdateAsync(current, version, cancellationToken);
        Exception? failure = null;
        try { if (await readiness(updated, cancellationToken)) return updated; failure = new InvalidOperationException($"Release '{updated.Version}' failed readiness validation."); }
        catch (Exception exception) { failure = exception; }
        if (!ReferenceEquals(updated, current))
        {
            try { await updater.RollbackAsync(updated, CancellationToken.None); }
            catch (Exception rollback) { throw new AggregateException("Release readiness failed and host rollback also failed.", failure!, rollback); }
        }
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure!).Throw();
        throw failure!;
    }
}
