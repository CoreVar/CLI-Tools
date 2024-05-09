using CoreVar.CommandLineInterface.Runtime;

namespace CoreVar.CommandLineInterface.Interfaces;

public interface IHelpExecutor
{

    ValueTask ShowHelp(CommandTreeContext context);

}
