﻿using CoreVar.CommandLineInterface.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
