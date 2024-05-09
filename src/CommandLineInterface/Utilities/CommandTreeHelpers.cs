using CoreVar.CommandLineInterface.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace CoreVar.CommandLineInterface.Utilities;

public static class CommandTreeHelpers
{

    public static string GetCommandName(IServiceProvider services, CommandTreeContext context)
    {
        var appContext = services.GetRequiredService<IApplicationContext>();
        return (appContext.IsReplMode ? context.Root.Child! : context.Root).ToString();
    }

    public static CommandTreeElementContext GetExecutingCommand(this CommandTreeElementContext context)
        => context.Child is null ? context : GetExecutingCommand(context.Child);


    public static IEnumerable<CommandTreeValidationResult> Validate(this CommandTreeContext commandTreeContext)
    {
        var executeCommand = GetExecutingCommand(commandTreeContext.Root);
        if (executeCommand.Element.Options is not null)
        {
            var requiredOptions = new HashSet<string>();
            foreach (var option in executeCommand.Element.Options)
                if (option.Value.IsRequired)
                    requiredOptions.Add(option.Key);

            var existingOptions = new HashSet<string>();
            if (executeCommand.Options is not null)
                foreach (var optionKey in executeCommand.Options.Keys)
                    existingOptions.Add(optionKey);

            requiredOptions.ExceptWith(existingOptions);

            foreach (var requiredOptionName in requiredOptions)
            {
                var option = executeCommand.Element.Options[requiredOptionName];
                yield return new CommandTreeValidationResult
                {
                    Element = executeCommand.Element,
                    ElementContext = executeCommand,
                    Message = $"The '{requiredOptionName}' option is required.",
                    Option = option
                };
            }
        }

        if (executeCommand.Element.Arguments is not null)
        {
            for (var i = 0; i < executeCommand.Element.Arguments.Count; i++)
            {
                var argument = executeCommand.Element.Arguments[i];
                if (!argument.IsRequired)
                    break;
                if (executeCommand.Arguments is not null && i < executeCommand.Arguments.Count)
                    continue;
                yield return new CommandTreeValidationResult
                {
                    Element = executeCommand.Element,
                    ElementContext = executeCommand,
                    Message = $"The '{argument.Name}' argument is required.",
                    Argument = argument
                };
            }
        }

        var argumentMap = new bool[commandTreeContext.Arguments.Length];
        var currentElement = commandTreeContext.Root;
        do
        {
            if (currentElement.Position < 0)
                continue;

            if (argumentMap[currentElement.Position])
                throw new InvalidOperationException("Cannot map command, argument already mapped.");
            argumentMap[currentElement.Position] = true;

            if (currentElement.Child is null)
            {
                if (currentElement.Options is not null)
                    foreach (var optionKvp in currentElement.Options)
                    {
                        if (argumentMap[optionKvp.Value.Position])
                            throw new InvalidOperationException("Cannot map option, argument already mapped.");
                        argumentMap[optionKvp.Value.Position] = true;
                        for (var i = 0; i < optionKvp.Value.ValueLength; i++)
                        {
                            var position = optionKvp.Value.Position + 1 + i;
                            if (argumentMap[position])
                                throw new InvalidOperationException("Cannot map option value, argument already mapped.");
                            argumentMap[position] = true;
                        }
                    }

                if (currentElement.Arguments is not null)
                    foreach (var argumentKvp in currentElement.Arguments)
                        for (var i = argumentKvp.Value.ValueRange.Start.Value; i < argumentKvp.Value.ValueRange.End.Value; i++)
                        {
                            if (argumentMap[i])
                                throw new InvalidOperationException("Cannot map argument value, argument already mapped.");
                            argumentMap[i] = true;
                        }

            }
        } while ((currentElement = currentElement!.Child) is not null);

        for (var i = 0; i < argumentMap.Length; i++)
        {
            if (argumentMap[i])
                continue;
            yield return new CommandTreeValidationResult
            {
                ArgumentsRange = new(i, i + 1),
                Message = $"The argument '{commandTreeContext.Arguments[i]}' is not recognized."
            };
        }
    }

}
