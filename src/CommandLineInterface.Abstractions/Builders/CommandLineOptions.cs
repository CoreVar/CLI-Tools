using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Builders;

public class CommandLineOptions
{

    public StringComparer CommandComparer { get; init; } = StringComparer.Ordinal;

    public StringComparer OptionComparer { get; init; } = StringComparer.Ordinal;

    /// <summary>Host policy allowing interactive prompts. Per-invocation policy can further restrict this.</summary>
    public bool EnablePrompts { get; set; } = true;

    public bool IsReplEnabled { get; set; }

    public string? ReplPrompt { get; set; }

    public string? InitializationPrompt { get; set; }

    public bool? ShowInitializationPromptForReplOnly { get; set; }

    public string? DefaultHelpOptionName { get; set; }

    public string[]? DefaultHelpOptionAliases { get; set; }

    public string? ExitCommandName { get; set; }

    /// <summary>The exit code used when a command handler throws.</summary>
    public int CommandErrorExitCode { get; set; } = 1;

    /// <summary>The exit code used when command-line validation fails.</summary>
    public int ValidationErrorExitCode { get; set; } = 2;

    /// <summary>The exit code used when execution is cancelled.</summary>
    public int CancellationExitCode { get; set; } = 130;

    /// <summary>Enables suggestions for misspelled commands and options.</summary>
    public bool EnableSuggestions { get; set; } = true;

    /// <summary>Enables the built-in version option.</summary>
    public bool EnableVersionOption { get; set; } = true;

    /// <summary>The version displayed by the built-in version option.</summary>
    public string? Version { get; set; }

    /// <summary>Environment-variable prefix used by opt-in environment binding.</summary>
    public string? EnvironmentPrefix { get; set; }

    /// <summary>Whether ANSI styling may be used when the terminal supports it.</summary>
    public bool UseColor { get; set; } = true;

    /// <summary>Optional localization hook for built-in CLI text.</summary>
    public Func<string, string>? Localize { get; set; }

    /// <summary>Maximum number of commands retained by the REPL history.</summary>
    public int ReplHistoryLimit { get; set; } = 100;

    /// <summary>Built-in REPL command that displays command history.</summary>
    public string? HistoryCommandName { get; set; } = "history";

}
