using CoreVar.CommandLineInterface.Runtime;

namespace CoreVar.CommandLineInterface.Utilities;

public class CommandTreeValidationResult
{

    public CommandTreeElement? Element { get; set; }

    public CommandTreeOption? Option { get; set; }

    public CommandTreeArgument? Argument { get; set; }

    public CommandTreeElementContext? ElementContext { get; set; }

    public CommandTreeOptionContext? OptionContext { get; set; }

    public CommandTreeArgumentContext? ArgumentContext { get; set; }

    public Range? ArgumentsRange { get; set; }

    public required string Message { get; init; }

}
