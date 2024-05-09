using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Runtime;

public class CommandTreeArgument(string name)
{
    public string Name => name;

    public string? Description { get; set; }

    public required GetArgumentValueDelegate GetValueHandler { get; set; }
    
    public bool IsRequired { get; set; }
    
    public List<Action<IHostApplicationBuilder>>? HostBuilders { get; set; }
    
    public List<Action<IHost>>? HostSetups { get; set; }
}
