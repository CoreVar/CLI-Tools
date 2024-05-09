using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Support;

public class CommandBuilder(string name, CommandLineOptions commandLineOptions) : ICommandBuilder, ICommandBuilderInternals
{
    private readonly List<Action<IHostApplicationBuilder>> _hostBuilders = [];
    private readonly List<Action<IHost>> _hostSetups = [];

    string IBuilderInternals.Name => name;

    string? IBuilderInternals.DisplayName { get; set; }

    string? IBuilderInternals.Description { get; set; }

    Func<CommandExecutionContext, ValueTask>? IExecutableBuilderInternals.ExecuteDelegate { get; set; }

    Dictionary<string, Action<ICommandBuilder>> IParentBuilderInternals.Children { get; } = new(commandLineOptions.CommandComparer);

    CommandLineOptions IBuilderInternals.CommandLineOptions => commandLineOptions;

    Dictionary<string, ICommandOptionBuilder> IExecutableBuilderInternals.Options { get; } = new(commandLineOptions.OptionComparer);

    List<ICommandArgumentBuilder> IExecutableBuilderInternals.Arguments { get; } = [];

    List<Action<IHostApplicationBuilder>>? IBuilderInternals.HostBuilders => _hostBuilders;

    List<Action<IHost>>? IBuilderInternals.HostSetups => _hostSetups;

    IHelpOptionBuilder? IExecutableBuilderInternals.HelpOption { get; set; }
    
    bool IExecutableBuilderInternals.DisableHelp { get; set; }
    
    List<CommandTreeElementUsage>? IExecutableBuilderInternals.Usages { get; set; }

    void IBuilderInternals.AddHostBuilder(Action<IHostApplicationBuilder> handler)
        => _hostBuilders.Add(handler);

    void IBuilderInternals.AddHostSetup(Action<IHost> handler)
        => _hostSetups.Add(handler);

}
