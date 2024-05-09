using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;

namespace CoreVar.CommandLineInterface.Support;

public class HelpOptionBuilder(CommandLineOptions commandLineOptions) : IHelpOptionBuilder, IHelpOptionBuilderInternals
{

    string? IHelpOptionBuilderInternals.Option { get; set; }

    HashSet<string>? IHelpOptionBuilderInternals.Aliases { get; set; }

    CommandLineOptions IHelpOptionBuilderInternals.CommandLineOptions => commandLineOptions;
}
