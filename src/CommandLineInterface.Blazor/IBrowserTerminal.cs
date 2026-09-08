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
