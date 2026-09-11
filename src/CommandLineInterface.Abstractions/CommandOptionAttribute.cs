using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = false)]
public class CommandOptionAttribute(string name) : Attribute
{
    public bool PromptIfMissing { get; set; }

    public string? PromptLabel { get; set; }

    public bool Secret { get; set; }



    public string Name => name;

    public string? EnvironmentVariable { get; set; }

    public string? ConfigurationKey { get; set; }

    public bool Global { get; set; }

    public bool Hidden { get; set; }

    public string? Deprecated { get; set; }

    public string[] Completions { get; set; } = [];

}
