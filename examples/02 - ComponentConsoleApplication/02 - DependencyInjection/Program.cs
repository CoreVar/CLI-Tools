using ConsoleApplication;
using ConsoleApplication.Services;
using CoreVar.CommandLineInterface;
using Microsoft.Extensions.DependencyInjection;

await CliApp.RunAsync(builder =>
{
    builder
        .EnableRepl()
        .SetupHostBuilder(hostBuilder =>
        {
            hostBuilder.Services.AddSingleton<ServiceStatus>();
        });

    builder.Components<ConsoleApplicationContext>();
});