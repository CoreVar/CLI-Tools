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
