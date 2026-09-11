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

    bool PromptIfMissing { get; set; }

    string? PromptLabel { get; set; }

    bool Secret { get; set; }

    bool IsRequired { get; set; }

    HashSet<string>? Aliases { get; set; }

    object? DefaultValue { get; set; }

    string? EnvironmentVariable { get; set; }

    string? ConfigurationKey { get; set; }

    bool IsGlobal { get; set; }

    bool IsHidden { get; set; }

    string? DeprecationMessage { get; set; }

    List<Func<object?, string?>> Validators { get; }

    List<string> Completions { get; }

}
