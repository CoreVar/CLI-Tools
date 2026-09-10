using System.Diagnostics;

namespace CoreVar.CommandLineInterface.Distribution;

/// <summary>Delivers a bounded credential envelope through an anonymous stdin pipe, never argv or a temporary file.</summary>
public static class ProtectedProcessHandoff
{
    /// <summary>Arguments must be non-secret. Child output is discarded, including on failure. The caller owns and clears the credential buffer.</summary>
    public static async ValueTask<int> RunAsync(string executable, IReadOnlyList<string> arguments,
        ReadOnlyMemory<byte> credentialEnvelope, TimeSpan timeout, string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (!Path.IsPathFullyQualified(executable) || !File.Exists(executable) ||
            credentialEnvelope.Length is < 1 or > 1_048_576 || timeout <= TimeSpan.Zero || timeout > TimeSpan.FromHours(1))
            throw new InstallationRunException("handoff-invalid");
        cancellationToken.ThrowIfCancellationRequested();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (workingDirectory is not null) start.WorkingDirectory = Path.GetFullPath(workingDirectory);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        var started = false;
        Task stdout = Task.CompletedTask, stderr = Task.CompletedTask;
        try
        {
            started = process.Start();
            if (!started) throw new InstallationRunException("handoff-start-failed");
            // Drain both pipes concurrently so a noisy child cannot deadlock the handoff.
            stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, deadline.Token);
            stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, deadline.Token);
            await process.StandardInput.BaseStream.WriteAsync(credentialEnvelope, deadline.Token);
            await process.StandardInput.BaseStream.FlushAsync(deadline.Token);
            process.StandardInput.Close(); // EOF frames the single envelope.
            await process.WaitForExitAsync(deadline.Token);
            await Task.WhenAll(stdout, stderr);
            return process.ExitCode;
        }
        catch
        {
            if (started && !process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await process.WaitForExitAsync(cleanup.Token);
                }
                catch { throw new InstallationRunException("handoff-termination-uncertain"); }
            }
            if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
            throw new InstallationRunException(deadline.IsCancellationRequested ? "handoff-timeout" : "handoff-failed");
        }
        finally
        {
            deadline.Cancel();
            try { await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(5)); }
            catch { /* Never surface child output or pipe errors containing sensitive context. */ }
        }
    }
}
