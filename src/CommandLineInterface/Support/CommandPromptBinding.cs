using CoreVar.CommandLineInterface.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreVar.CommandLineInterface.Support;

internal static class CommandPromptBinding
{
    public static bool HasSecrets(CommandTreeElementContext executing) =>
        executing.Element.Options?.Values.Any(option => option.Secret) == true ||
        executing.Element.Arguments?.Any(argument => argument.Secret) == true;

    public static async ValueTask FillAsync(CommandTreeElementContext executing, CommandExecutionContext context)
    {
        // A declared scripting flag is respected without reserving a new parser namespace.
        if (executing.Options?.TryGetValue("--no-prompt", out var noPrompt) == true && !noPrompt.Option.AcceptsValue)
            context.EnablePrompts = false;

        foreach (var option in executing.Element.Options?.Values ?? Enumerable.Empty<CommandTreeOption>())
        {
            if (!option.PromptIfMissing || executing.Options?.ContainsKey(option.Name) == true) continue;
            if (option.DefaultValue is not null ||
                (option.EnvironmentVariable is not null && Environment.GetEnvironmentVariable(option.EnvironmentVariable) is not null) ||
                (option.ConfigurationKey is not null && context.Services.GetService<IConfiguration>()?[option.ConfigurationKey] is not null)) continue;
            if (!option.AcceptsValue) throw new CommandPromptException();
            var value = await context.PromptAsync(new CommandPromptRequest(option.PromptLabel ?? option.Name, option.Secret));
            var supplied = new CommandTreeOptionContext { Option = option, Position = -1, ValueLength = 0 };
            supplied.Values.Add(value);
            executing.Options ??= new(context.CommandTreeContext.Tree.CommandLineOptions.OptionComparer);
            executing.Options.Add(option.Name, supplied);
        }
        foreach (var argument in executing.Element.Arguments ?? Enumerable.Empty<CommandTreeArgument>())
        {
            if (!argument.PromptIfMissing || argument.DefaultValue is not null || executing.Arguments?.ContainsKey(argument.Name) == true) continue;
            var value = await context.PromptAsync(new CommandPromptRequest(argument.PromptLabel ?? argument.Name, argument.Secret));
            var supplied = new CommandTreeArgumentContext { Argument = argument, ValueRange = 0..0 };
            supplied.Values.Add(value);
            executing.Arguments ??= [];
            executing.Arguments.Add(argument.Name, supplied);
        }
    }
}
