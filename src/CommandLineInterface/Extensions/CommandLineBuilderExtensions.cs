using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Support;
using CoreVar.CommandLineInterface.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

public static partial class BuilderExtensions
{

    public static ICommandLineBuilder EnableRepl(this ICommandLineBuilder builder, bool enable = true)
    {
        var commandLineBuilderInternals = (ICommandLineBuilderInternals)builder;
        commandLineBuilderInternals.CommandLineOptions.IsReplEnabled = enable;
        return builder;
    }

    public static ICommandLineBuilder ReplPrompt(this ICommandLineBuilder builder, string prompt)
    {
        var commandLineBuilderInternals = (ICommandLineBuilderInternals)builder;
        commandLineBuilderInternals.CommandLineOptions.ReplPrompt = prompt;
        return builder;
    }

    public static ICommandLineBuilder InitializationPrompt(this ICommandLineBuilder builder, string prompt, bool replOnly = true)
    {


        return builder;
    }

    public static ICommandLineBuilder ExitCommand(this ICommandLineBuilder builder, string name)
    {
        var commandLineBuilderInternals = (ICommandLineBuilderInternals)builder;
        commandLineBuilderInternals.CommandLineOptions.ExitCommandName = name;
        return builder;
    }

    public static ICommandLineBuilder DefaultHelpOption(this ICommandLineBuilder builder, string name, params string[]? aliases)
    {
        var builderInternals = (ICommandLineBuilderInternals)builder;
        builderInternals.CommandLineOptions.DefaultHelpOptionName = name;
        builderInternals.CommandLineOptions.DefaultHelpOptionAliases = aliases;

        return builder;
    }

}