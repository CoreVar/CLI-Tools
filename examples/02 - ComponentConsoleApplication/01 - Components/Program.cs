using ConsoleApplication;
using CoreVar.CommandLineInterface;

await CliApp.RunAsync(cliApp =>
{
    cliApp
        .EnableRepl()
        .Components<ConsoleApplicationContext>();
});