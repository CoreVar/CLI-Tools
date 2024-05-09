using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.Server;
using CoreVar.CommandLineInterface;

namespace ConsoleApplication.Services;

public class AspNetCoreWebHost : IAsyncDisposable
{
    private WebApplication? _app;

    public async ValueTask StartAsync(CommandExecutionContext commandContext)
    {
        if (_app is not null)
            return;
        var builder = WebApplication.CreateSlimBuilder();

        builder.Logging.ClearProviders();

        _app = builder.Build();

        _app.MapGet("/", () => "Hello World!");

        await _app.StartAsync();

        var server = _app.Services.GetService<IServer>();
        var addresses = server?.Features.Get<IServerAddressesFeature>();
        var hostAddress = (addresses?.Addresses ?? []).FirstOrDefault();

        await commandContext.Console.WriteLine(hostAddress ?? "No host address supplied");
    }

    public async ValueTask StopAsync()
    {
        if (_app is null)
            return;
        await _app.StopAsync();
        await _app.DisposeAsync();
        _app = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is not null)
            await _app.DisposeAsync();
    }

}
