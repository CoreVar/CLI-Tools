using CoreVar.CommandLineInterface.Runtime;

namespace CoreVar.CommandLineInterface.Builders.Internals;

public delegate bool GetArgumentValueDelegate(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value);

public interface ICommandArgumentBuilderInternals : IBuilderInternals
{

    GetArgumentValueDelegate GetValueHandler { get; set; }

    bool IsRequired { get; set; }

}
