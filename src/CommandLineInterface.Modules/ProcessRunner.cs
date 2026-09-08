using System.Diagnostics;

namespace CoreVar.CommandLineInterface.Modules;

internal static class ProcessRunner
{
    public static async ValueTask<int> RunAsync(string executable, IEnumerable<string> arguments,
        string workingDirectory, IReadOnlyDictionary<string, string?>? environment,
        CancellationToken cancellationToken) => await RunAsync(executable, arguments, workingDirectory, environment, null, cancellationToken);

    public static async ValueTask<int> RunAsync(string executable, IEnumerable<string> arguments,
        string workingDirectory, IReadOnlyDictionary<string, string?>? environment,
        ModuleInvocationContext? invocation, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            WorkingDirectory = workingDirectory,
            RedirectStandardInput = invocation?.StandardInput is not null,
            RedirectStandardOutput = invocation is not null,
            RedirectStandardError = invocation is not null
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        if (environment is not null)
            foreach (var item in environment) start.Environment[item.Key] = item.Value;
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start '{executable}'.");
        using var inputCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            Task[] outputPumps = invocation is null ? [] :
            [
                PumpAsync(process.StandardOutput, invocation.Console.WriteLine, invocation.MaximumOutputCharacters, cancellationToken),
                PumpAsync(process.StandardError, invocation.Console.WriteErrorLine, invocation.MaximumOutputCharacters, cancellationToken)
            ];
            var inputPump = invocation?.StandardInput is null
                ? Task.CompletedTask
                : PumpInputAsync(process, invocation.StandardInput, inputCancellation.Token);
            await process.WaitForExitAsync(cancellationToken);
            inputCancellation.Cancel();
            await Task.WhenAll(outputPumps);
            try { await inputPump; } catch (OperationCanceledException) when (inputCancellation.IsCancellationRequested) { }
            return process.ExitCode;
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(true);
            throw;
        }
    }

    private static async Task PumpAsync(StreamReader reader, Func<string, ValueTask> write, int maximumCharacters, CancellationToken cancellationToken)
    {
        var remaining = Math.Max(0, maximumCharacters);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (remaining <= 0) continue;
            var emitted = line.Length <= remaining ? line : line[..remaining];
            await write(emitted);
            remaining -= line.Length;
            if (remaining <= 0) await write("[module output truncated]");
        }
    }

    private static async Task PumpInputAsync(Process process, Func<CancellationToken, ValueTask<string?>> read, CancellationToken cancellationToken)
    {
        while (!process.HasExited && await read(cancellationToken) is { } line)
            await process.StandardInput.WriteLineAsync(line.AsMemory(), cancellationToken);
        process.StandardInput.Close();
    }
}
