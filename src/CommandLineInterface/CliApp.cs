using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CoreVar.CommandLineInterface;

public class CliApp(IHost host) : IAsyncDisposable
{

    public void Run() 
        => RunAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask RunAsync()
        => await host.RunAsync(((ApplicationContext)host.Services.GetRequiredService<IApplicationContext>()).RuntimeCancellationTokenSource.Token).ConfigureAwait(false);

    public async ValueTask StopAsync()
        => await host.StopAsync();

    public async ValueTask StartAsync()
        => await host.StartAsync();

    public IHost Host => host;

    public static async ValueTask RunAsync(Action<ICommandLineBuilder> builder)
    {
        var commandLineBuilder = CommandLineBuilder.Create();
        builder(commandLineBuilder);
        var cliApp = commandLineBuilder.Build();
        await cliApp.RunAsync();

        await cliApp.DisposeAsync();
    }

    public ValueTask DisposeAsync()
    {
        host.Dispose();
        return ValueTask.CompletedTask;
    }
}