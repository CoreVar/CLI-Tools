using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public class CommandOptionAliasAttribute(string alias) : Attribute
{

    public string Alias => alias;

}
