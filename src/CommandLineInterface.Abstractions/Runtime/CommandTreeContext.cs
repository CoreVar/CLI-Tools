namespace CoreVar.CommandLineInterface.Runtime;

public class CommandTreeContext
{

    public required CommandTreeElementContext Root { get; init; }

    public required CommandTree Tree { get; init; }

    public required string[] Arguments { get; init; }

}
