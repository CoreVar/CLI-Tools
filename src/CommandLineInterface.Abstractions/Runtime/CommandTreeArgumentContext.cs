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

    /// <summary>Raw values assigned to this positional argument.</summary>
    public List<string> Values { get; } = [];

    /// <summary>Gets the original token positions assigned to this argument.</summary>
    public List<int> Positions { get; } = [];

}
