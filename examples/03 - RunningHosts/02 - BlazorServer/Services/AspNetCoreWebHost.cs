using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.Server;
using CoreVar.CommandLineInterface;
using ConsoleApplication.Components;

namespace ConsoleApplication.Services;

public class AspNetCoreWebHost : IAsyncDisposable
{
    private WebApplication? _app;

    public async ValueTask StartAsync(CommandExecutionContext commandContext)
    {
        if (_app is not null)
            return;

        var builder = WebApplication.CreateBuilder();

        builder.Logging.ClearProviders();

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        _app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!_app.Environment.IsDevelopment())
        {
            _app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            _app.UseHsts();
        }

        _app.UseHttpsRedirection();

        _app.UseStaticFiles();
        _app.UseAntiforgery();

        _app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

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
