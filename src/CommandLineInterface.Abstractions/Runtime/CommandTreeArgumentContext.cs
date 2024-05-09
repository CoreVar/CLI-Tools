using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Runtime;

public class CommandTreeArgumentContext
{

    public required CommandTreeArgument Argument { get; init; }

    public required Range ValueRange { get; init; }

}
