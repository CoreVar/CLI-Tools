using CoreVar.CommandLineInterface.Runtime;

namespace CoreVar.CommandLineInterface.Builders.Internals;

public delegate bool GetArgumentValueDelegate(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value);

public interface ICommandArgumentBuilderInternals : IBuilderInternals
{

    GetArgumentValueDelegate GetValueHandler { get; set; }

    bool PromptIfMissing { get; set; }

    string? PromptLabel { get; set; }

    bool Secret { get; set; }

    bool IsRequired { get; set; }

    object? DefaultValue { get; set; }

    bool IsVariadic { get; set; }

    List<Func<object?, string?>> Validators { get; }

    List<string> Completions { get; }

}
