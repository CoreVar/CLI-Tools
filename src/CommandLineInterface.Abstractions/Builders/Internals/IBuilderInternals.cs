using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoreVar.CommandLineInterface.Builders.Internals;

public interface IBuilderInternals
{

    string Name { get; }

    string? DisplayName { get; set; }

    public string? Description { get; set; }

    void AddHostBuilder(Action<IHostApplicationBuilder> handler);

    void AddHostSetup(Action<IHost> handler);

    List<Action<IHostApplicationBuilder>>? HostBuilders { get; }

    List<Action<IHost>>? HostSetups { get; }

    CommandLineOptions CommandLineOptions { get; }

}