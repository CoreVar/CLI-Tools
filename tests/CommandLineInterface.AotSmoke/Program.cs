using CoreVar.CommandLineInterface;
using System.Linq;

await CliApp.RunAsync(app => app.Command("hello", command =>
{
    var names = command.Argument<string[]>("names").Variadic();
    var count = command.Option<int>("--count").Default(1);
    command.OnExecute(context =>
        context.Console.WriteLine(string.Join(",", context.GetArgument(names).Take(context.GetOption(count)))));
}), args);
