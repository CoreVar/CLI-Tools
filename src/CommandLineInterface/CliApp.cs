using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoreVar.CommandLineInterface;

/// <summary>
/// Contains the built application and is responsible for running it and executing commands.
/// </summary>
/// <param name="host">The built <see cref="IHost"/> object containing the services to run the application.</param>
public class CliApp(IHost host) : IAsyncDisposable
{

    /// <summary>
    /// Runs the command line application synchronously.
    /// </summary>
    public void Run() 
        => RunAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>
    /// Runs the command line application asynchronously.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    public async ValueTask RunAsync()
        => await host.RunAsync(((ApplicationContext)host.Services.GetRequiredService<IApplicationContext>()).RuntimeCancellationTokenSource.Token).ConfigureAwait(false);

    /// <summary>
    /// Stops the command line application asynchronously.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    public async ValueTask StopAsync()
        => await host.StopAsync();

    /// <summary>
    /// Starts the command line application asynchronously.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    public async ValueTask StartAsync()
        => await host.StartAsync();

    /// <summary>
    /// The <see cref="IHost"/> object containing the services to run the command line application.
    /// </summary>
    public IHost Host => host;

    /// <summary>
    /// Creates a builder and runs the command line application asynchronously.
    /// </summary>
    /// <param name="builder">A delegate for building the command line application.</param>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    public static async ValueTask RunAsync(Action<ICommandLineBuilder> builder)
    {
        var commandLineBuilder = CommandLineBuilder.Create();
        builder(commandLineBuilder);
        var cliApp = commandLineBuilder.Build();
        await cliApp.RunAsync();

        await cliApp.DisposeAsync();
    }

    /// <summary>
    /// Creates a builder and runs the command line application asynchronously.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="args"></param>
    /// <returns></returns>
    public static async ValueTask RunAsync(Action<ICommandLineBuilder> builder, string[] args)
    {
        var commandLineBuilder = CommandLineBuilder.Create();
        builder(commandLineBuilder);
        var cliApp = commandLineBuilder.Build(() => args);
        await cliApp.RunAsync();
        await cliApp.DisposeAsync();
    }

    /// <summary>
    /// Disposes the command line application.
    /// </summary>
    /// <returns>A <see cref="ValueTask"/> for the asynchronous operation.</returns>
    public ValueTask DisposeAsync()
    {
        host.Dispose();
        return ValueTask.CompletedTask;
    }
}