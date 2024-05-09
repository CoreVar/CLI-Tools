using CoreVar.CommandLineInterface;

await CliApp.RunAsync(cliApp =>
{
    cliApp
        .Command("start", startCommand =>
        {
            startCommand
                .Description("Starts the service.")
                .OnExecute(async context =>
                {
                    await context.Console.WriteLine("Service started");

                    // Oh no! An error happened, let's return an error code
                    return -1;
                });
        })
        .Command("status", statusCommand =>
        {
            statusCommand
                .Description("Gets the status of the service.")
                .OnExecute(async context =>
                {
                    await context.Console.WriteLine("Getting the current status...");

                    // Oh no! An error happened, let's throw an exception
                    throw new InvalidOperationException("This isn't supposed to happen.");
                });
        })
        .Command("stop", stopCommand =>
        {
            stopCommand
                .Description("Stops the service.")
                .OnExecute(async context =>
                {
                    await context.Console.WriteLine("Service stopped.");

                    // Oh no! An error happened, let's return an error code
                    context.Result = -1;
                });
        });
});