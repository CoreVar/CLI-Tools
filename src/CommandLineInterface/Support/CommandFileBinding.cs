using CoreVar.CommandLineInterface.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreVar.CommandLineInterface.Support;

internal static class CommandFileBinding
{
    public static async ValueTask FillAsync(CommandTreeElementContext executing, CommandExecutionContext context)
    {
        var prefix = "inputs/" + context.CommandTreeContext.Root.ToString().Replace(' ', '/');
        foreach (var option in executing.Element.Options?.Values ?? Enumerable.Empty<CommandTreeOption>())
        {
            if (!option.InputFile) continue;
            CommandTreeOptionContext? supplied = null;
            executing.Options?.TryGetValue(option.Name, out supplied);
            if (supplied is not null && supplied.Values.Count == 0) continue; // Explicit malformed input is never a picker request.
            var value = supplied?.Values.LastOrDefault();
            if (value is null && option.EnvironmentVariable is not null) value = Environment.GetEnvironmentVariable(option.EnvironmentVariable);
            if (value is null && option.ConfigurationKey is not null) value = context.Services.GetService<IConfiguration>()?[option.ConfigurationKey];
            value ??= option.DefaultValue as string;
            var path = await context.Files.ResolveReadPathAsync(new(value)
            {
                Label = option.PromptLabel ?? option.Name,
                SuggestedPath = prefix + "/options/" + option.Name.TrimStart('-')
            });
            if (supplied is null)
            {
                supplied = new() { Option = option, Position = -1, ValueLength = 0 };
                executing.Options ??= new(context.CommandTreeContext.Tree.CommandLineOptions.OptionComparer);
                executing.Options.Add(option.Name, supplied);
            }
            supplied.Values.Clear();
            supplied.Values.Add(path);
        }
        foreach (var argument in executing.Element.Arguments ?? Enumerable.Empty<CommandTreeArgument>())
        {
            if (!argument.InputFile) continue;
            CommandTreeArgumentContext? supplied = null;
            executing.Arguments?.TryGetValue(argument.Name, out supplied);
            var path = await context.Files.ResolveReadPathAsync(new(supplied?.Values.LastOrDefault() ?? argument.DefaultValue as string)
            {
                Label = argument.PromptLabel ?? argument.Name,
                SuggestedPath = prefix + "/arguments/" + argument.Name
            });
            if (supplied is null)
            {
                supplied = new() { Argument = argument, ValueRange = 0..0 };
                executing.Arguments ??= [];
                executing.Arguments.Add(argument.Name, supplied);
            }
            supplied.Values.Clear();
            supplied.Values.Add(path);
        }
    }
}
