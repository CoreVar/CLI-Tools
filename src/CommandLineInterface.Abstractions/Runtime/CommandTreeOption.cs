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
    public string Name => name;

    public string? Description { get; set; }

    public bool AcceptsValue => acceptsValue;

    public string? ValueLabel { get; set; }

    public required GetOptionValueDelegate GetValueHandler { get; init; }
    
    public bool IsRequired { get; set; }
    
    public HashSet<string>? Aliases { get; set; }
    
    public List<Action<IHost>>? HostSetups { get; set; }

    public List<Action<IHostApplicationBuilder>>? HostBuilders { get; set; }
}
