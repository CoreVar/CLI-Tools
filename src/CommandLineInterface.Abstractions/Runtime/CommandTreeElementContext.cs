using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Runtime;

public class CommandTreeElementContext(CommandTreeElement element, int position, CommandTreeElementContext? child)
{

    public CommandTreeElement Element { get; } = element;

    public int Position => position;

    public CommandTreeElementContext? Child { get; } = child;

    public Dictionary<string, CommandTreeOptionContext>? Options { get; set; }

    public Dictionary<string, CommandTreeArgumentContext>? Arguments { get; set; }
    public bool HasHelpOption { get; set; }

    public override string ToString()
        => Child is null ? Element.Name : $"{Element.Name} {Child}";

}