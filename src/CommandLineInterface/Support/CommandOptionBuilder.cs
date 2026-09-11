using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using Microsoft.Extensions.Hosting;

namespace CoreVar.CommandLineInterface.Support;

public abstract class CommandOptionBuilder(string name, CommandLineOptions commandLineOptions) : ICommandOptionBuilder, ICommandOptionBuilderInternals
{
    private List<Action<IHostApplicationBuilder>>? _hostBuilders;
    private List<Action<IHost>>? _hostSetups;

    string IBuilderInternals.Name => name;

    bool ICommandOptionBuilderInternals.AcceptsValue { get; set; }

    string? IBuilderInternals.DisplayName { get; set; }

    string? IBuilderInternals.Description { get; set; }

    List<Action<IHostApplicationBuilder>>? IBuilderInternals.HostBuilders => _hostBuilders;

    List<Action<IHost>>? IBuilderInternals.HostSetups => _hostSetups;

    GetOptionValueDelegate ICommandOptionBuilderInternals.GetValueHandler { get; set; } = default!;

    bool ICommandOptionBuilderInternals.PromptIfMissing { get; set; }

    string? ICommandOptionBuilderInternals.PromptLabel { get; set; }

    bool ICommandOptionBuilderInternals.Secret { get; set; }

    bool ICommandOptionBuilderInternals.IsRequired { get; set; }

    HashSet<string>? ICommandOptionBuilderInternals.Aliases { get; set; }

    object? ICommandOptionBuilderInternals.DefaultValue { get; set; }

    string? ICommandOptionBuilderInternals.EnvironmentVariable { get; set; }

    string? ICommandOptionBuilderInternals.ConfigurationKey { get; set; }

    bool ICommandOptionBuilderInternals.IsGlobal { get; set; }

    bool ICommandOptionBuilderInternals.IsHidden { get; set; }

    string? ICommandOptionBuilderInternals.DeprecationMessage { get; set; }

    List<Func<object?, string?>> ICommandOptionBuilderInternals.Validators { get; } = [];

    List<string> ICommandOptionBuilderInternals.Completions { get; } = [];

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

public class CommandOptionBuilder<T>(string name, CommandLineOptions commandLineOptions) : CommandOptionBuilder(name, commandLineOptions), ICommandOptionBuilder<T>
{



}
