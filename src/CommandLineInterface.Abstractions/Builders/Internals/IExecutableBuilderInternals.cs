using CoreVar.CommandLineInterface.Runtime;
using Microsoft.Extensions.Hosting;
using CoreVar.CommandLineInterface.Execution;
using CoreVar.CommandLineInterface.Validation;

namespace CoreVar.CommandLineInterface.Builders.Internals;

public interface IExecutableBuilderInternals : IBuilderInternals
{

    Func<CommandExecutionContext, ValueTask>? ExecuteDelegate { get; set; }

    Dictionary<string, ICommandOptionBuilder> Options { get; }

    List<ICommandArgumentBuilder> Arguments { get; }

    IHelpOptionBuilder? HelpOption { get; set; }

    bool DisableHelp { get; set; }

    List<CommandTreeElementUsage>? Usages { get; set; }

    List<CommandMiddleware> Middleware { get; }

    List<Func<CommandExecutionContext, ValueTask<ValidationResult>>> Validators { get; }

    HashSet<string> Aliases { get; }

    bool IsHidden { get; set; }

    string? DeprecationMessage { get; set; }
}
