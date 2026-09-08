namespace CoreVar.CommandLineInterface;

/// <summary>Terminal dimensions reported by the browser viewport.</summary>
public readonly record struct TerminalSize(int Columns, int Rows);

/// <summary>Exposes terminal capabilities that are meaningful to interactive handlers.</summary>
public interface IBrowserTerminal
{
    TerminalSize Size { get; }
    event EventHandler<TerminalSize>? SizeChanged;
    bool IsInputSecret { get; set; }
}

/// <summary>A browser terminal session. The default implementation binds directly to one CLI REPL.</summary>
public interface IBrowserTerminalSession
{
    BlazorConsoleControl Console { get; }
    ValueTask SubmitAsync(string input, CancellationToken cancellationToken = default);
    ValueTask InterruptAsync(CancellationToken cancellationToken = default);
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
    ValueTask ResizeAsync(TerminalSize size, CancellationToken cancellationToken = default);
}

/// <summary>Safe default session that forwards input only to the configured CLI command tree.</summary>
public sealed class BoundCliTerminalSession(BlazorConsoleControl console) : IBrowserTerminalSession
{
    public BlazorConsoleControl Console { get; } = console;
    public ValueTask SubmitAsync(string input, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.SubmitLine(input);
        return ValueTask.CompletedTask;
    }

    public ValueTask InterruptAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.CancelLine();
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.Clear();
        return ValueTask.CompletedTask;
    }

    public ValueTask ResizeAsync(TerminalSize size, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.Resize(size.Columns, size.Rows);
        return ValueTask.CompletedTask;
    }
}

/// <summary>Helpers for input that must not be echoed or retained by a browser.</summary>
public static class BrowserTerminalExtensions
{
    public static async ValueTask<string> ReadSecretAsync(this IConsoleControl console, string? prompt = null)
    {
        if (!string.IsNullOrEmpty(prompt)) await console.Write(prompt);
        if (console is not IBrowserTerminal terminal) return await console.ReadLine();
        terminal.IsInputSecret = true;
        try { return await console.ReadLine(); }
        finally { terminal.IsInputSecret = false; }
    }
}
