using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;

namespace CoreVar.CommandLineInterface.Extensions;

public static class HelpOptionBuilderExtensions
{

    /// <summary>
    /// Defines help alias(es) for the current help option.
    /// </summary>
    /// <param name="builder">The help option builder.</param>
    /// <param name="aliases">The aliases.</param>
    /// <returns>The help option builder.</returns>
    public static IHelpOptionBuilder WithAlias(this IHelpOptionBuilder builder, params string[] aliases)
    {
        var builderInternals = (IHelpOptionBuilderInternals)builder;
        builderInternals.Aliases ??= new(builderInternals.CommandLineOptions.OptionComparer);

        foreach (var alias in aliases)
            builderInternals.Aliases.Add(alias);

        return builder;
    }

}
