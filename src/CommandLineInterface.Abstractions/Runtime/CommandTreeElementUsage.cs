using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Runtime;

public class CommandTreeElementUsage
{

    public required Func<IServiceProvider, string> Usage { get; init; }

    public string? Description { get; init; }

}
