using CoreVar.CommandLineInterface.Builders.Internals;

namespace CoreVar.CommandLineInterface;

public abstract class CommandLineComponent : ICommandLineComponentInternals
{

    public CommandExecutionContext Context { get; private set; } = default!;

    public IConsoleControl Console => Context.Console;

    void ICommandLineComponentInternals.Initialize(CommandExecutionContext context)
    {
        Context = context;
    }
}
