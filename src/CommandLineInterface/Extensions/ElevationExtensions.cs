using CoreVar.CommandLineInterface.Elevation;
using Microsoft.Extensions.DependencyInjection;

namespace CoreVar.CommandLineInterface;

public static partial class BuilderExtensions
{
    /// <summary>Returns whether the current CLI process already has elevated privileges.</summary>
    public static bool IsElevated(this CommandExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Services.GetRequiredService<IElevationService>().IsElevated;
    }

    /// <summary>Runs a command using Windows UAC or sudo when elevation is required.</summary>
    public static ValueTask<ElevationResult> RunElevatedAsync(
        this CommandExecutionContext context,
        string fileName,
        IEnumerable<string>? arguments = null,
        string? workingDirectory = null,
        bool waitForExit = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var request = new ElevationRequest(fileName, arguments?.ToArray() ?? [])
        {
            WorkingDirectory = workingDirectory,
            WaitForExit = waitForExit
        };
        var effectiveToken = cancellationToken == default ? context.CancellationToken : cancellationToken;
        return context.Services.GetRequiredService<IElevationService>().RunAsync(request, effectiveToken);
    }

    /// <summary>Relaunches the current CLI process using Windows UAC or sudo.</summary>
    public static ValueTask<ElevationResult> RelaunchElevatedAsync(
        this CommandExecutionContext context,
        IEnumerable<string>? arguments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var effectiveToken = cancellationToken == default ? context.CancellationToken : cancellationToken;
        return context.Services.GetRequiredService<IElevationService>()
            .RelaunchAsync(arguments?.ToArray() ?? context.Arguments, effectiveToken);
    }
}
