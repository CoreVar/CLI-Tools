namespace CoreVar.CommandLineInterface.Elevation;

/// <summary>Runs commands through the operating system's native privilege-elevation mechanism.</summary>
public interface IElevationService
{
    /// <summary>Whether the current process already has elevated privileges.</summary>
    bool IsElevated { get; }

    /// <summary>Runs a process with elevated privileges.</summary>
    ValueTask<ElevationResult> RunAsync(ElevationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Relaunches the current executable with elevated privileges.</summary>
    ValueTask<ElevationResult> RelaunchAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}
