using ConsoleApplication.Services;
using CoreVar.CommandLineInterface;

await CliApp.RunAsync(builder =>
{
    builder
        .EnableRepl()
        .SetupHostBuilder(hostBuilder =>
        {
            hostBuilder.Services.AddSingleton<AspNetCoreWebHost>();
        });

    builder
        .Command("start", startCommand =>
        {
            startCommand.OnExecute(async context =>
            {
                var webHost = context.Services.GetRequiredService<AspNetCoreWebHost>();
                await webHost.StartAsync(context);
            });
        })
        .Command("stop", stopCommand =>
        {
            stopCommand.OnExecute(async context =>
            {
                var webHost = context.Services.GetRequiredService<AspNetCoreWebHost>();
                await webHost.StopAsync();
            });
        });
});