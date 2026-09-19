using CoreVar.CommandLineInterface.Builders;
using Microsoft.Extensions.Hosting;
using CoreVar.CommandLineInterface.Execution;
using CoreVar.CommandLineInterface.Validation;

namespace CoreVar.CommandLineInterface.Runtime;

public class CommandTreeElement(CommandLineOptions commandLineOptions, string name)
{

    public string Name { get; set; } = name;

    public List<Action<IHostApplicationBuilder>>? HostBuilders { get; set; }

    public List<Action<IHost>>? HostSetups { get; set; }

    public Dictionary<string, CommandTreeOption>? Options { get; set; }

    public Dictionary<string, string>? OptionAliases { get; set; }

    public List<CommandTreeArgument>? Arguments { get; set; }

    public Func<CommandExecutionContext, ValueTask>? ExecuteDelegate { get; set; }

    public Dictionary<string, CommandTreeElement> Children { get; } = new(commandLineOptions.CommandComparer);

    public string? Description { get; set; }

    public List<CommandTreeElementUsage>? Usages { get; set; }

    public Func<string, bool>? HelpOptionComparer { get; set; }
    
    public bool DisableHelp { get; set; }

    public List<CommandMiddleware> Middleware { get; set; } = [];

    public List<Func<CommandExecutionContext, ValueTask<ValidationResult>>> Validators { get; set; } = [];

    public HashSet<string> Aliases { get; set; } = [];

    public bool IsHidden { get; set; }

    public string? DeprecationMessage { get; set; }
}
