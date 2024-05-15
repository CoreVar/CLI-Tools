using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;

namespace CoreVar.CommandLineInterface;

public static partial class BuilderExtensions
{

    /// <summary>
    /// Enables Read-Execute-Print Loop.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="enable">Enable REPL.</param>
    /// <returns>The builder.</returns>
    public static ICommandLineBuilder EnableRepl(this ICommandLineBuilder builder, bool enable = true)
    {
        var commandLineBuilderInternals = (ICommandLineBuilderInternals)builder;
        commandLineBuilderInternals.CommandLineOptions.IsReplEnabled = enable;
        return builder;
    }

    /// <summary>
    /// Defines the prompt text while in REPL mode.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="prompt">The prompt text.</param>
    /// <returns>The builder.</returns>
    public static ICommandLineBuilder ReplPrompt(this ICommandLineBuilder builder, string prompt)
    {
        var commandLineBuilderInternals = (ICommandLineBuilderInternals)builder;
        commandLineBuilderInternals.CommandLineOptions.ReplPrompt = prompt;
        return builder;
    }

    /// <summary>
    /// Defines an initialization prompt.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="prompt">The prompt.</param>
    /// <param name="replOnly">Whether the initialization prompt is for REPL mode only.</param>
    /// <returns>The builder.</returns>
    public static ICommandLineBuilder InitializationPrompt(this ICommandLineBuilder builder, string prompt, bool replOnly = true)
    {


        return builder;
    }

    /// <summary>
    /// Defines the exit command name.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="name">The exit command name.</param>
    /// <returns>The builder.</returns>
    public static ICommandLineBuilder ExitCommand(this ICommandLineBuilder builder, string name)
    {
        var commandLineBuilderInternals = (ICommandLineBuilderInternals)builder;
        commandLineBuilderInternals.CommandLineOptions.ExitCommandName = name;
        return builder;
    }

    /// <summary>
    /// Defines the default help option for the application.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="name">The help option name.</param>
    /// <param name="aliases">Default aliases for the help option.</param>
    /// <returns>The builder.</returns>
    public static ICommandLineBuilder DefaultHelpOption(this ICommandLineBuilder builder, string name, params string[]? aliases)
    {
        var builderInternals = (ICommandLineBuilderInternals)builder;
        builderInternals.CommandLineOptions.DefaultHelpOptionName = name;
        builderInternals.CommandLineOptions.DefaultHelpOptionAliases = aliases;

        return builder;
    }

}