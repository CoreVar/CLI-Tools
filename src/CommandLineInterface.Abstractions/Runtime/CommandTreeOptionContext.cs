using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Runtime;

public class CommandTreeOptionContext
{

    public required CommandTreeOption Option { get; init; }

    public required int Position { get; init; }

    public required int ValueLength { get; init; }

}
