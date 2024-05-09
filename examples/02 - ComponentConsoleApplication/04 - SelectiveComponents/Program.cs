using ConsoleApplication;
using CoreVar.CommandLineInterface;

await CliApp.RunAsync(builder =>
{
    builder
        .EnableRepl()
        .Command("host", hostCommand =>
        {
            hostCommand.Component(ConsoleApplicationContext.Start);
            hostCommand.Component(ConsoleApplicationContext.Stop);
        })
        .Command("ops", opsCommand =>
        {
            opsCommand.Component(ConsoleApplicationContext.Status);
        });
});