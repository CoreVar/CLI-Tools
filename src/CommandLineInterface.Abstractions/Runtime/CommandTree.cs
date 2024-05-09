using CoreVar.CommandLineInterface.Builders;

namespace CoreVar.CommandLineInterface.Runtime;

public class CommandTree
{

    public required CommandTreeElement Root { get; init; }

    public required CommandLineOptions CommandLineOptions { get; init; }

}
