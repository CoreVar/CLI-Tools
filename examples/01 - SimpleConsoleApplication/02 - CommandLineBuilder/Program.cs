using CoreVar.CommandLineInterface;

var builder = CommandLineBuilder.Create();

builder
    .Command("start", startCommand =>
    {
        startCommand
            .Description("Starts the service.")
            .OnExecute(() =>
            {
                Console.WriteLine("Service started");
            });
    })
    .Command("stop", stopCommand =>
    {
        stopCommand
            .Description("Stops the service.")
            .OnExecute(() =>
            {
                Console.WriteLine("Service stopped.");
            });
    });

var cliApp = builder.Build();

await cliApp.RunAsync();