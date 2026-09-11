using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Runtime;

public class CommandTreeOption(string name, bool acceptsValue)
{
    public bool PromptIfMissing { get; set; }

    public string? PromptLabel { get; set; }

    public bool Secret { get; set; }


    public string Name => name;

    public string? Description { get; set; }

    public bool AcceptsValue => acceptsValue;

    public string? ValueLabel { get; set; }

    public required GetOptionValueDelegate GetValueHandler { get; init; }
    
    public bool IsRequired { get; set; }
    
    public HashSet<string>? Aliases { get; set; }
    
    public List<Action<IHost>>? HostSetups { get; set; }

    public List<Action<IHostApplicationBuilder>>? HostBuilders { get; set; }

    public object? DefaultValue { get; set; }

    public string? EnvironmentVariable { get; set; }

    public string? ConfigurationKey { get; set; }

    public bool IsGlobal { get; set; }

    public bool IsHidden { get; set; }

    public string? DeprecationMessage { get; set; }

    public List<Func<object?, string?>> Validators { get; set; } = [];

    public List<string> Completions { get; set; } = [];
}
