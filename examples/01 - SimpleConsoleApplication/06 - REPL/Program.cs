using CoreVar.CommandLineInterface;

await CliApp.RunAsync(cliApp =>
{
    cliApp
        .EnableRepl() // This option enables REPL, everything else should work exactly the same

        .Command("start", startCommand =>
        {
            startCommand
                .Description("Starts the service.")
                .OnExecute(async context =>
                {
                    await context.Console.WriteLine("Service started");
                });
        })
        .Command("stop", stopCommand =>
        {
            stopCommand
                .Description("Stops the service.")
                .OnExecute(async context =>
                {
                    await context.Console.WriteLine("Service stopped.");
                });
        });

});