using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Extensions;

public static class HelpOptionBuilderExtensions
{

    public static IHelpOptionBuilder WithAlias(this IHelpOptionBuilder builder, params string[] aliases)
    {
        var builderInternals = (IHelpOptionBuilderInternals)builder;
        builderInternals.Aliases ??= new(builderInternals.CommandLineOptions.OptionComparer);

        foreach (var alias in aliases)
            builderInternals.Aliases.Add(alias);

        return builder;
    }

}
