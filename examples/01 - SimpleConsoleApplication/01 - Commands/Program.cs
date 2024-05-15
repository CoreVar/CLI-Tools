using CoreVar.CommandLineInterface;

await CliApp.RunAsync(cliApp =>
{
    cliApp
        .Command("start", startCommand =>
        {
            startCommand
                .OnExecute(() => Console.WriteLine("Service started"));
        })
        .Command("stop", stopCommand =>
        {
            stopCommand
                .OnExecute(() => Console.WriteLine("Service stopped."));
        });
});