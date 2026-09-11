using System.Text.Json;
using CoreVar.CommandLineInterface;

if (args is ["--corevar-describe"])
{
    Console.WriteLine(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "corevar.module.json")));
    return;
}

await CliApp.RunAsync(cli => cli.Command("hello", command =>
{
    var name = command.Argument<string>("name").IsOptional();
    command.Description("Greets someone from the module.")
        .OnExecute(context => context.Console.WriteLine($"Hello, {context.GetArgument(name) ?? "world"}!"));
}), args);
