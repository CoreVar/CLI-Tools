using CoreVar.CommandLineInterface.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Builders.Internals;

public delegate bool GetOptionValueDelegate(CommandExecutionContext context, CommandTreeOptionContext option, out object value);

public interface ICommandOptionBuilderInternals : IBuilderInternals
{

    bool AcceptsValue { get; set; }

    GetOptionValueDelegate GetValueHandler { get; set; }

    bool IsRequired { get; set; }

    HashSet<string>? Aliases { get; set; }

}
