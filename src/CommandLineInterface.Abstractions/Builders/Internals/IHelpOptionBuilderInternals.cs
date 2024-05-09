using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Builders.Internals;

public interface IHelpOptionBuilderInternals
{

    string? Option { get; set; }

    HashSet<string>? Aliases { get; set; }

    CommandLineOptions CommandLineOptions { get; }

}
