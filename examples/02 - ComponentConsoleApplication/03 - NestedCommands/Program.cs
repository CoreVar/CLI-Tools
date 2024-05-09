
using ConsoleApplication;
using CoreVar.CommandLineInterface;

await CliApp.RunAsync(builder =>
{
    builder
        .EnableRepl()
        .Components<ConsoleApplicationContext>();
});