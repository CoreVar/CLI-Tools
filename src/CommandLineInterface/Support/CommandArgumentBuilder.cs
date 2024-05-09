using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Support;

public class CommandArgumentBuilder<T>(string name, CommandLineOptions commandLineOptions) : ICommandArgumentBuilder<T>, ICommandArgumentBuilderInternals
{
    private List<Action<IHostApplicationBuilder>>? _hostBuilders;
    private List<Action<IHost>>? _hostSetups;

    string IBuilderInternals.Name => name;

    string? IBuilderInternals.DisplayName { get; set; }

    string? IBuilderInternals.Description { get; set; }

    List<Action<IHostApplicationBuilder>>? IBuilderInternals.HostBuilders => _hostBuilders;

    List<Action<IHost>>? IBuilderInternals.HostSetups => _hostSetups;

    GetArgumentValueDelegate ICommandArgumentBuilderInternals.GetValueHandler { get; set; } = default!;

    bool ICommandArgumentBuilderInternals.IsRequired { get; set; }

    CommandLineOptions IBuilderInternals.CommandLineOptions => commandLineOptions;

    void IBuilderInternals.AddHostBuilder(Action<IHostApplicationBuilder> handler)
    {
        _hostBuilders ??= [];
        _hostBuilders.Add(handler);
    }

    void IBuilderInternals.AddHostSetup(Action<IHost> handler)
    {
        _hostSetups ??= [];
        _hostSetups.Add(handler);
    }
}
