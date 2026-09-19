using CoreVar.CommandLineInterface;
using CommandLineInterface.AotSmoke;

await CliApp.RunAsync(app => app.Components<SmokeContext>(), args);
