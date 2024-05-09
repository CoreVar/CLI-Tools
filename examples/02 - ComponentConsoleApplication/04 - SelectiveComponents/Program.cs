using ConsoleApplication;
using CoreVar.CommandLineInterface;

await CliApp.RunAsync(builder =>
{
    builder
        .EnableRepl()
        .Command("host", hostCommand =>
        {
            hostCommand.Component(ConsoleApplicationContext.Default.Start);
            hostCommand.Component(ConsoleApplicationContext.Default.Stop);
        })
        .Command("ops", opsCommand =>
        {
            opsCommand.Component(ConsoleApplicationContext.Default.Status);
        });
});