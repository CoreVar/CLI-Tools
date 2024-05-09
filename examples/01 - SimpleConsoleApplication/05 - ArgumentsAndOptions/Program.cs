using CoreVar.CommandLineInterface;

await CliApp.RunAsync(cliApp =>
{
    cliApp
        .Command("start", startCommand =>
        {
            var jobNameArgument = startCommand.Argument<string>("jobName")
                .Description("Name of the job");

            var options = (
                startCommand.Option<int?>("--delay")
                    .Description("Delay the startup of the service"),
                startCommand.Option("--log")
                    .Description("Turns on logging"));
            
            startCommand
                .Description("Starts the service.")
                .OnExecute(async context =>
                {
                    var jobName = context.GetArgument(jobNameArgument);

                    var (delay, log) = context.GetOptions(options);

                    if (log)
                        await context.Console.WriteLine("Logging is enabled");

                    if (delay.HasValue)
                    {
                        await context.Console.WriteLine($"Delaying startup by {delay} seconds...");
                        await Task.Delay(TimeSpan.FromSeconds(delay.Value));
                    }

                    await context.Console.WriteLine($"Starting job '{jobName}'");
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