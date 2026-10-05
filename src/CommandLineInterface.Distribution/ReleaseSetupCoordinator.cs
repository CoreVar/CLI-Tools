namespace CoreVar.CommandLineInterface.Distribution;

/// <summary>Updates a direct installation, validates product readiness, and restores the previous host on failure.</summary>
public sealed class ReleaseSetupCoordinator(DirectUpdater updater)
{
    public async ValueTask<InstallationState> UpdateAndVerifyAsync(InstallationState current,
        Func<InstallationState, CancellationToken, ValueTask<bool>> readiness, string? version = null,
        CancellationToken cancellationToken = default)
    {
        return await updater.UpdateAsync(current, version, cancellationToken, readiness);
    }

    public static async ValueTask<InstallationState> RunAsync(InstallationState current,
        Func<InstallationState, CancellationToken, ValueTask<InstallationState>> update,
        Func<InstallationState, CancellationToken, ValueTask<InstallationState>> rollback,
        Func<InstallationState, CancellationToken, ValueTask<bool>> readiness, CancellationToken cancellationToken = default)
    {
        var updated = await update(current, cancellationToken);
        Exception? failure = null;
        try { if (await readiness(updated, cancellationToken)) return updated; failure = new InvalidOperationException($"Release '{updated.Version}' failed readiness validation."); }
        catch (Exception exception) { failure = exception; }
        if (!ReferenceEquals(updated, current))
        {
            try { await rollback(updated, CancellationToken.None); }
            catch (Exception rollbackFailure) { throw new AggregateException("Release readiness failed and host rollback also failed.", failure!, rollbackFailure); }
        }
        System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure!).Throw();
        throw failure!;
    }
}
