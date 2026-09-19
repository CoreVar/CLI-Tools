using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;

namespace CoreVar.CommandLineInterface;

public static class CommandFileExtensions
{
    /// <summary>Resolves a readable filename before execution, using the host's file provider.</summary>
    public static ICommandOptionBuilder<string> InputFile(this ICommandOptionBuilder<string> builder, string? label = null)
    {
        var metadata = (ICommandOptionBuilderInternals)builder;
        metadata.InputFile = true;
        metadata.PromptLabel = label;
        return builder;
    }

    public static ICommandArgumentBuilder<string> InputFile(this ICommandArgumentBuilder<string> builder, string? label = null)
    {
        var metadata = (ICommandArgumentBuilderInternals)builder;
        metadata.InputFile = true;
        metadata.PromptLabel = label;
        return builder;
    }
}
