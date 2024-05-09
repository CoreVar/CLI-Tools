using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Builders;

public class CommandLineOptions
{

    public StringComparer CommandComparer { get; init; } = StringComparer.Ordinal;

    public StringComparer OptionComparer { get; init; } = StringComparer.Ordinal;

    public bool IsReplEnabled { get; set; }

    public string? ReplPrompt { get; set; }

    public string? DefaultHelpOptionName { get; set; }

    public string[]? DefaultHelpOptionAliases { get; set; }

    public string? ExitCommandName { get; set; }

}
