using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
public class CommandArgumentAttribute(string name) : Attribute
{

    public string Name => name;

    public int Index { get; set; } = -1;

}