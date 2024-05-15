using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Interfaces;
using CoreVar.CommandLineInterface.Runtime;
using CoreVar.CommandLineInterface.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace CoreVar.CommandLineInterface;

/// <summary>
/// Builds the definition for command line applications.
/// </summary>
/// <param name="name">The name of the executable for the CLI app.</param>
/// <param name="options">The command line options.</param>
public class CommandLineBuilder(string name, CommandLineOptions options) : ICommandLineBuilder, ICommandLineBuilderInternals
{
    private readonly List<Action<IHostApplicationBuilder>> _hostBuilderDelegates = [];
    private readonly List<Action<IHost>> _hostSetups = [];

    /// <summary>
    /// Creates a <see cref="CommandLineBuilder"/> instance for building the definition of a command line application.
    /// </summary>
    /// <returns>The <see cref="CommandLineBuilder"/> instance.</returns>
    public static CommandLineBuilder Create()
        => Create(Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]));

    /// <summary>
    /// Creates a <see cref="CommandLineBuilder"/> instance for building the definition of a command line application.
    /// </summary>
    /// <param name="options">The command line options.</param>
    /// <returns>The <see cref="CommandLineBuilder"/> instance.</returns>
    public static CommandLineBuilder Create(CommandLineOptions options)
        => new(Path.GetFileNameWithoutExtension(Environment.GetCommandLineArgs()[0]), options);

    /// <summary>
    /// Creates a <see cref="CommandLineBuilder"/> instance for building the definition of a command line application.
    /// </summary>
    /// <param name="name">The name of the executable for the CLI app.</param>
    /// <returns>The <see cref="CommandLineBuilder"/> instance.</returns>
    public static CommandLineBuilder Create(string name)
        => Create(name, new());

    /// <summary>
    /// Creates a <see cref="CommandLineBuilder"/> instance for building the definition of a command line application.
    /// </summary>
    /// <param name="name">The name of the executable for the CLI app.</param>
    /// <param name="options">The command line options.</param>
    /// <returns>The <see cref="CommandLineBuilder"/> instance.</returns>
    public static CommandLineBuilder Create(string name, CommandLineOptions options)
        => new(name, options);

    /// <summary>
    /// Builds the definition into a CliApp that can be run.
    /// </summary>
    /// <returns>The <see cref="CliApp"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Gets thrown if REPL is enabled and an execute handler exists for the root application.</exception>
    public CliApp Build()
    {
        var hostBuilder = new HostApplicationBuilder();
        var builderInternals = (ICommandLineBuilderInternals)this;

        if (options.IsReplEnabled && builderInternals.ExecuteDelegate is not null)
            throw new InvalidOperationException("Cannot have a command executor at the root level when REPL is enabled.");

        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        var useRepl = args.Length == 0 && options.IsReplEnabled;

        _ = hostBuilder.Logging.AddFilter<ConsoleLoggerProvider>((category, level)
            => category switch
            {
                "Microsoft.Extensions.Hosting.Internal.Host" or
                "Microsoft.Hosting.Lifetime"
                    => level >= LogLevel.Warning,
                _ => true,
            }
        );

        hostBuilder.Services
            .AddSingleton(options)
            .AddSingleton<IConsoleControl, NativeConsoleControl>()
            .AddSingleton<CommandExecutionService>()
            .AddSingleton<ICommandExecutor>(sp => sp.GetRequiredService<CommandExecutionService>())
            .AddScoped<CommandExecutionContext>();

        CommandTree commandTree;

        if (useRepl)
        {
            commandTree = CommandTreeBuilder.Build(this);
            hostBuilder.Services
                .AddScoped<CommandTreeContextState>()
                .AddScoped(sp => sp.GetRequiredService<CommandTreeContextState>().CommandTreeContext);
        }
        else
        {
            var context = CommandTreeBuilder.BuildAndLoad(this, args);
            commandTree = context.Tree;
            hostBuilder.Services
                .AddSingleton(context);
        }

        hostBuilder.Services.AddSingleton(commandTree);

        var applicationContext = new ApplicationContext(useRepl, this);

        hostBuilder.Services
            .AddSingleton<IApplicationContext>(applicationContext);

        RunHostBuilders(hostBuilder, commandTree.Root);

        hostBuilder.Services.AddHostedService(sp => sp.GetRequiredService<CommandExecutionService>());

        if (!hostBuilder.Services.Any(d => d.ServiceType == typeof(IHelpExecutor)))
            hostBuilder.Services.AddSingleton<IHelpExecutor, HelpExecutor>();

        var host = hostBuilder.Build();

        RunHostSetup(host, commandTree.Root);

        return new(host);
    }

    private static void RunHostBuilders(IHostApplicationBuilder hostBuilder, CommandTreeElement element)
    {
        if (element.HostBuilders is not null)
            foreach (var handler in element.HostBuilders)
                handler(hostBuilder);

        if (element.Options is not null)
            foreach (var handler in element.Options)
                if (handler.Value.HostBuilders is not null)
                    foreach (var builder in handler.Value.HostBuilders)
                        builder(hostBuilder);

        foreach (var child in element.Children)
            RunHostBuilders(hostBuilder, child.Value);
    }

    private static void RunHostSetup(IHost host, CommandTreeElement element)
    {
        if (element.HostSetups is not null)
            foreach (var handler in element.HostSetups)
                handler(host);

        foreach (var child in element.Children)
            RunHostSetup(host, child.Value);
    }

    string IBuilderInternals.Name => name;

    string? IBuilderInternals.DisplayName { get; set; }

    string? IBuilderInternals.Description { get; set; }

    void IBuilderInternals.AddHostBuilder(Action<IHostApplicationBuilder> handler)
        => _hostBuilderDelegates.Add(handler);

    void IBuilderInternals.AddHostSetup(Action<IHost> handler)
        => _hostSetups.Add(handler);

    Dictionary<string, Action<ICommandBuilder>> IParentBuilderInternals.Children { get; } = new(options.CommandComparer);

    Func<CommandExecutionContext, ValueTask>? IExecutableBuilderInternals.ExecuteDelegate { get; set; }

    CommandLineOptions IBuilderInternals.CommandLineOptions => options;

    List<Action<IHostApplicationBuilder>>? IBuilderInternals.HostBuilders => _hostBuilderDelegates;

    List<Action<IHost>>? IBuilderInternals.HostSetups => _hostSetups;

    Dictionary<string, ICommandOptionBuilder> IExecutableBuilderInternals.Options { get; } = new(options.OptionComparer);

    List<ICommandArgumentBuilder> IExecutableBuilderInternals.Arguments { get; } = [];

    IHelpOptionBuilder? IExecutableBuilderInternals.HelpOption { get; set; }

    bool IExecutableBuilderInternals.DisableHelp { get; set; }

    List<CommandTreeElementUsage>? IExecutableBuilderInternals.Usages { get; set; }
}
