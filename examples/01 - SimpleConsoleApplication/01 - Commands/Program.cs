using CoreVar.CommandLineInterface;

await CliApp.RunAsync(cliApp =>
{
    cliApp
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
});