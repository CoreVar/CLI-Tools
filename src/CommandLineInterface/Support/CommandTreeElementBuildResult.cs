using CoreVar.CommandLineInterface.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Support;

public class CommandTreeElementBuildResult
{

    public bool IsLastCommand { get; init; }

    public required CommandTreeElement Element { get; init; }

}
