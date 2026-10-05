using CoreVar.CommandLineInterface;

namespace CommandLineInterface.AotSmoke;

[CommandName("hello")]
public sealed class HelloComponent : CommandLineComponent
{
    public Task ExecuteAsync(
        [CommandArgument("names", Variadic = true)] string[] names,
        [CommandOption("--count")] int count,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Console.WriteLine(string.Join(",", names.Take(count)));
        return Task.CompletedTask;
    }
}

[Component<HelloComponent>]
public partial class SmokeContext : ComponentContext;
