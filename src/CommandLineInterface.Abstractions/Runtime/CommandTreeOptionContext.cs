using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Runtime;

public class CommandTreeOptionContext
{

    public required CommandTreeOption Option { get; init; }

    public required int Position { get; set; }

    public required int ValueLength { get; set; }

    /// <summary>All values supplied for this option, in command-line order.</summary>
    public List<string> Values { get; } = [];

    /// <summary>The positions of repeated occurrences of this option.</summary>
    public List<int> Positions { get; } = [];

}
