namespace CoreVar.CommandLineInterface.Elevation;

/// <summary>Describes a process that should run with elevated privileges.</summary>
public sealed record ElevationRequest(string FileName, IReadOnlyList<string> Arguments)
{
    /// <summary>The working directory for the elevated process.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Wait for the elevated process and capture its exit code.</summary>
    public bool WaitForExit { get; init; } = true;
}

/// <summary>The outcome of an elevation request.</summary>
public sealed record ElevationResult(bool Started, bool Canceled, int? ExitCode, string? Error = null)
{
    /// <summary>Whether the process started, was not cancelled, and returned a successful exit code when one is available.</summary>
    public bool Succeeded => Started && !Canceled && (ExitCode is null or 0);

    /// <summary>Creates the result returned when a user declines an elevation prompt.</summary>
    public static ElevationResult UserCanceled() => new(false, true, null);
}
