using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Builders.Internals;

public interface IApplicationContextInternals
{

    ICommandLineBuilder CommandLineBuilder { get; }

}
