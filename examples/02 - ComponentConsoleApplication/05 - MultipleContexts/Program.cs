using ConsoleApplication;
using CoreVar.CommandLineInterface;

await CliApp.RunAsync(builder =>
{
    builder
        .EnableRepl()
        .Command("host", hostCommand =>
            hostCommand.Components<HostContext>())
        .Command("ops", opsCommand =>
            opsCommand.Components<OpsContext>());
});