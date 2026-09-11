using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
public class CommandArgumentAttribute(string name) : Attribute
{
    public bool PromptIfMissing { get; set; }

    public string? PromptLabel { get; set; }

    public bool Secret { get; set; }



    public string Name => name;

    public int Index { get; set; } = -1;

    public bool Variadic { get; set; }

    public string[] Completions { get; set; } = [];

}
