using CoreVar.CommandLineInterface.Runtime;
using Microsoft.Extensions.Hosting;

namespace CoreVar.CommandLineInterface.Builders.Internals;

public interface IExecutableBuilderInternals : IBuilderInternals
{

    Func<CommandExecutionContext, ValueTask>? ExecuteDelegate { get; set; }

    Dictionary<string, ICommandOptionBuilder> Options { get; }

    List<ICommandArgumentBuilder> Arguments { get; }

    IHelpOptionBuilder? HelpOption { get; set; }

    bool DisableHelp { get; set; }

    List<CommandTreeElementUsage>? Usages { get; set; }
}