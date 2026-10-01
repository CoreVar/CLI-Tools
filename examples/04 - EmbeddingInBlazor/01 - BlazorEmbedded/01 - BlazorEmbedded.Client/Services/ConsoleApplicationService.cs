using CoreVar.CommandLineInterface;

namespace BlazorApplication.Client.Services;

public class ConsoleApplicationService(GreetingService greetingService)
{
    public CliApp? App { get; private set; }

    public bool IsRunning => App is not null;

    public async ValueTask StartAsync()
    {
        if (App is not null)
            return;

        var builder = CommandLineBuilder.Create("blazor");

        builder.EnableRepl();

        builder.SetupHostBuilder(hostBuilder =>
        {
            hostBuilder.AddBlazorConsoleControl();
            
            hostBuilder.Services.AddSingleton(greetingService);
        });

        builder.Command("say", sayCommand =>
        {
            var textArgument = sayCommand.Argument<string>("text");

            sayCommand.OnExecute(async context =>
            {
                var greetingService = context.Services.GetRequiredService<GreetingService>();
                var text = context.GetArgument(textArgument);
                greetingService.SetGreeting(text);

                await context.Console.WriteLine("Greeting has been set.");
            });
        });

        builder.Command("wait", waitCommand => waitCommand.OnExecute(async context =>
        {
            await context.Console.WriteLine("Waiting for 30 seconds. Press Ctrl+C to interrupt.");
            await Task.Delay(TimeSpan.FromSeconds(30), context.CancellationToken);
        }));

        builder.Command("demo", demoCommand => demoCommand.OnExecute(async context =>
        {
            await context.Console.WriteLine("\u001b[32;1mANSI color is enabled.\u001b[0m");
            for (var progress = 0; progress <= 100; progress += 25)
            {
                await context.Console.Write($"Progress: {progress,3}%\r");
                await Task.Delay(80, context.CancellationToken);
            }
            await context.Console.WriteLine("Progress: 100%");
        }));

        App = builder.Build();

        await App.StartAsync();
    }

    public async ValueTask StopAsync()
    {
        if (App is null)
            return;
        await App.StopAsync();
        await App.DisposeAsync();
        App = null;
    }

}
