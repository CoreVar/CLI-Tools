using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Runtime;
using System.Xml.Linq;

namespace CoreVar.CommandLineInterface.Support;

public class CommandTreeBuilder
{

    private static void AddOptionContext(ref Dictionary<string, CommandTreeOptionContext>? contexts, CommandTreeOption option, int position, string[] arguments)
    {
        contexts ??= [];
        if (!contexts.TryGetValue(option.Name, out var context))
        {
            context = new CommandTreeOptionContext { Option = option, Position = position, ValueLength = option.AcceptsValue ? 1 : 0 };
            contexts.Add(option.Name, context);
        }
        context.Position = position;
        context.ValueLength = option.AcceptsValue ? 1 : 0;
        context.Positions.Add(position);
        if (option.AcceptsValue && position + 1 < arguments.Length)
            context.Values.Add(arguments[position + 1]);
    }

    private static void AddArgumentContext(ref Dictionary<string, CommandTreeArgumentContext>? contexts, CommandTreeArgument argument, int start, int end, string[] arguments)
    {
        contexts ??= [];
        if (contexts.TryGetValue(argument.Name, out var existing))
        {
            for (var index = start; index < end; index++)
            {
                existing.Values.Add(arguments[index]);
                existing.Positions.Add(index);
            }
            return;
        }
        var context = new CommandTreeArgumentContext { Argument = argument, ValueRange = new Range(start, end) };
        for (var index = start; index < end; index++)
        {
            context.Values.Add(arguments[index]);
            context.Positions.Add(index);
        }
        contexts.Add(argument.Name, context);
    }

    private static CommandTreeElement BuildElement(IParentBuilder builder)
    {
        var parentBuilderInternals = (IParentBuilderInternals)builder;
        var executableBuilderInternals = (IExecutableBuilderInternals)builder;

        var element = new CommandTreeElement(parentBuilderInternals.CommandLineOptions, parentBuilderInternals.Name)
        {
            Description = parentBuilderInternals.Description,
            ExecuteDelegate = executableBuilderInternals.ExecuteDelegate,
            Usages = executableBuilderInternals.Usages,
            Middleware = [.. executableBuilderInternals.Middleware],
            Validators = [.. executableBuilderInternals.Validators],
            Aliases = new(executableBuilderInternals.Aliases, parentBuilderInternals.CommandLineOptions.CommandComparer),
            IsHidden = executableBuilderInternals.IsHidden,
            DeprecationMessage = executableBuilderInternals.DeprecationMessage,

            HostBuilders = parentBuilderInternals.HostBuilders,
            HostSetups = parentBuilderInternals.HostSetups
        };

        return element;
    }

    private static void BuildCommandElements(CommandTreeElement element, IExecutableBuilder builder)
    {
        var builderInternals = (IExecutableBuilderInternals)builder;

        element.Options ??= new(builderInternals.CommandLineOptions.OptionComparer);
        element.Arguments ??= [];

        foreach (var optionKvp in builderInternals.Options)
            BuildCommandOption(element, optionKvp.Value);

        foreach (var argument in builderInternals.Arguments)
            element.Arguments!.Add(BuildArgument(argument));
    }

    private static CommandTreeOption BuildOption(ICommandOptionBuilder builder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        var option = new CommandTreeOption(builderInternals.Name, builderInternals.AcceptsValue)
        {
            GetValueHandler = builderInternals.GetValueHandler,
            IsRequired = builderInternals.IsRequired,
            PromptIfMissing = builderInternals.PromptIfMissing,
            PromptLabel = builderInternals.PromptLabel,
            Secret = builderInternals.Secret,
            Aliases = builderInternals.Aliases,
            HostBuilders = builderInternals.HostBuilders,
            HostSetups = builderInternals.HostSetups,
            Description = builderInternals.Description
            ,DefaultValue = builderInternals.DefaultValue
            ,EnvironmentVariable = builderInternals.EnvironmentVariable
            ,ConfigurationKey = builderInternals.ConfigurationKey
            ,IsGlobal = builderInternals.IsGlobal
            ,IsHidden = builderInternals.IsHidden
            ,DeprecationMessage = builderInternals.DeprecationMessage
            ,Validators = [.. builderInternals.Validators]
            ,Completions = builderInternals.Secret ? [] : [.. builderInternals.Completions]
        };

        return option;
    }

    private static void BuildCommandOption(CommandTreeElement commandElement, ICommandOptionBuilder optionBuilder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)optionBuilder;
        var option = BuildOption(optionBuilder);
        commandElement.Options!.Add(builderInternals.Name, option);
        if (option.Aliases is not null)
        {
            commandElement.OptionAliases ??= new(builderInternals.CommandLineOptions.OptionComparer);
            foreach (var alias in option.Aliases)
                commandElement.OptionAliases.Add(alias, option.Name);
        }
    }

    public static CommandTreeArgument BuildArgument(ICommandArgumentBuilder builder)
    {
        var builderInternals = (ICommandArgumentBuilderInternals)builder;
        var argument = new CommandTreeArgument(builderInternals.Name)
        {
            GetValueHandler = builderInternals.GetValueHandler,
            IsRequired = builderInternals.IsRequired,
            PromptIfMissing = builderInternals.PromptIfMissing,
            PromptLabel = builderInternals.PromptLabel,
            Secret = builderInternals.Secret,
            HostBuilders = builderInternals.HostBuilders,
            HostSetups = builderInternals.HostSetups,
            Description = builderInternals.Description
            ,DefaultValue = builderInternals.DefaultValue
            ,IsVariadic = builderInternals.IsVariadic
            ,Validators = [.. builderInternals.Validators]
            ,Completions = builderInternals.Secret ? [] : [.. builderInternals.Completions]
        };

        return argument;
    }

    private static (CommandTreeElement Element, IParentBuilder Builder) BuildChild(CommandTreeElement parent, IParentBuilder parentBuilder, string commandName, Action<ICommandBuilder> builder)
    {
        var parentBuilderInternals = (IParentBuilderInternals)parentBuilder;
        var commandBuilder = new CommandBuilder(commandName, parentBuilderInternals.CommandLineOptions);
        builder(commandBuilder);
        var childElement = BuildElement(commandBuilder);
        if (parent.Options is not null)
        {
            foreach (var global in parent.Options.Where(pair => pair.Value.IsGlobal))
            {
                childElement.Options ??= new(parentBuilderInternals.CommandLineOptions.OptionComparer);
                childElement.Options.TryAdd(global.Key, global.Value);
                if (global.Value.Aliases is not null)
                {
                    childElement.OptionAliases ??= new(parentBuilderInternals.CommandLineOptions.OptionComparer);
                    foreach (var alias in global.Value.Aliases)
                        childElement.OptionAliases.TryAdd(alias, global.Key);
                }
            }
        }
        parent.Children.Add(commandName, childElement);
        foreach (var alias in childElement.Aliases)
            parent.Children.Add(alias, childElement);
        return (childElement, commandBuilder);
    }

    private static void BuildChildren(CommandTreeElement element, IParentBuilder parentBuilder)
    {
        BuildCommandElements(element, (IExecutableBuilder)parentBuilder);
        var parentBuilderInternals = (IParentBuilderInternals)parentBuilder;
        foreach (var commandKvp in parentBuilderInternals.Children)
        {
            var (childElement, childBuilder) = BuildChild(element, parentBuilder, commandKvp.Key, commandKvp.Value);
            BuildChildren(childElement, childBuilder);
        }
    }

    private static CommandTreeElementContext Load(CommandLineOptions commandLineOptions, CommandTreeElement element, int position, string[] arguments)
    {
        CommandTreeElementContext? child = null;
        var nextPosition = position + 1;
        Dictionary<string, CommandTreeOptionContext>? optionContexts = null;
        Dictionary<string, CommandTreeArgumentContext>? argumentContexts = null;
        var hasHelpOption = false;
        if (nextPosition < arguments.Length)
        {
            var argument = arguments[nextPosition];

            if (element.Children.TryGetValue(argument, out var childElement))
                child = Load(commandLineOptions, childElement, nextPosition, arguments);
            else
            {
                var currentArgumentPosition = 0;
                var endOfOptions = false;
                while (nextPosition < arguments.Length)
                {
                    argument = arguments[nextPosition];

                    if (!endOfOptions && argument == "--")
                    {
                        endOfOptions = true;
                        nextPosition++;
                        continue;
                    }

                    if (!endOfOptions && !element.DisableHelp)
                        if (element.HelpOptionComparer is not null)
                        {
                            if (element.HelpOptionComparer(argument))
                                hasHelpOption = true;
                        }
                        else
                        {
                            if (commandLineOptions.OptionComparer.Equals("--help", argument) ||
                                commandLineOptions.OptionComparer.Equals("-?", argument) ||
                                commandLineOptions.OptionComparer.Equals("-h", argument))
                                hasHelpOption = true;
                        }


                    if (!endOfOptions && element.OptionAliases is not null && element.OptionAliases.TryGetValue(argument, out var commandOptionName) == true)
                        argument = commandOptionName;

                    if (!endOfOptions && element.Options?.TryGetValue(argument, out var commandOption) == true)
                    {
                        AddOptionContext(ref optionContexts, commandOption, nextPosition, arguments);
                        if (commandOption.AcceptsValue)
                            nextPosition++;
                    }
                    else if (element.Arguments?.Count > currentArgumentPosition)
                    {
                        var commandArgument = element.Arguments[currentArgumentPosition++];
                        AddArgumentContext(ref argumentContexts, commandArgument, nextPosition, nextPosition + 1, arguments);
                        if (commandArgument.IsVariadic)
                            currentArgumentPosition--;
                    }
                    nextPosition++;
                }
            }

        }

        return new CommandTreeElementContext(element, position, child)
        {
            Options = optionContexts,
            Arguments = argumentContexts,
            HasHelpOption = hasHelpOption
        };
    }

    private static CommandTreeElementContext BuildAndLoad(CommandTreeElement element, IParentBuilder builder, int position, string[] arguments)
    {
        var parentBuilderInternals = (IParentBuilderInternals)builder;
        var executableBuilder = (IExecutableBuilder)builder;
        var executableBuilderInternals = (IExecutableBuilderInternals)builder;
        CommandTreeElementContext? child = null;
        var nextPosition = position + 1;
        Dictionary<string, CommandTreeOptionContext>? optionContexts = null;
        Dictionary<string, CommandTreeArgumentContext>? argumentContexts = null;
        var hasHelpOption = false;
        BuildCommandElements(element, executableBuilder);
        if (nextPosition < arguments.Length)
        {
            var commandKey = arguments[nextPosition];
            if (parentBuilderInternals.Children.TryGetValue(commandKey, out var childBuilderHandler))
            {
                var (childElement, childBuilder) = BuildChild(element, builder, commandKey, childBuilderHandler);
                child = BuildAndLoad(childElement, childBuilder, nextPosition, arguments);
            }
            else
            {
                int currentCommandArgumentPosition = 0;
                var endOfOptions = false;
                while (nextPosition < arguments.Length)
                {
                    commandKey = arguments[nextPosition];

                    if (!endOfOptions && commandKey == "--")
                    {
                        endOfOptions = true;
                        nextPosition++;
                        continue;
                    }

                    if (!endOfOptions && !(element.DisableHelp = executableBuilderInternals.DisableHelp))
                    {
                        if (executableBuilderInternals.HelpOption is not null)
                        {
                            element.HelpOptionComparer = argument =>
                            {
                                var helpOptionBuilderInternals = (IHelpOptionBuilderInternals)executableBuilderInternals.HelpOption;
                                if (parentBuilderInternals.CommandLineOptions.OptionComparer.Equals(helpOptionBuilderInternals.Option, argument) ||
                                    helpOptionBuilderInternals.Aliases?.Contains(argument) == true)
                                    return true;
                                return false;
                            };
                        }
                        else if (parentBuilderInternals.CommandLineOptions.DefaultHelpOptionName is not null)
                        {
                            element.HelpOptionComparer = argument =>
                            {
                                if (parentBuilderInternals.CommandLineOptions.OptionComparer.Equals(parentBuilderInternals.CommandLineOptions.DefaultHelpOptionName, argument) ||
                                    parentBuilderInternals.CommandLineOptions.DefaultHelpOptionAliases?.Any(a => parentBuilderInternals.CommandLineOptions.OptionComparer.Equals(a, argument)) == true)
                                    return true;
                                return false;
                            };
                        }
                    }

                    if (!endOfOptions && element.HelpOptionComparer is not null)
                    {
                        if (element.HelpOptionComparer(arguments[nextPosition]))
                            hasHelpOption = true;
                    }
                    else if (!endOfOptions)
                    {
                        if (parentBuilderInternals.CommandLineOptions.OptionComparer.Equals("--help", arguments[nextPosition]) ||
                            parentBuilderInternals.CommandLineOptions.OptionComparer.Equals("-?", arguments[nextPosition]) ||
                            parentBuilderInternals.CommandLineOptions.OptionComparer.Equals("-h", arguments[nextPosition]))
                            hasHelpOption = true;
                    }

                    if (!endOfOptions && element.OptionAliases is not null && element.OptionAliases.TryGetValue(commandKey, out var commandOptionName) == true)
                        commandKey = commandOptionName;

                    if (!endOfOptions && element.Options?.TryGetValue(commandKey, out var commandOption) == true)
                    {
                        AddOptionContext(ref optionContexts, commandOption, nextPosition, arguments);

                        if (commandOption.AcceptsValue)
                            nextPosition++;
                    }
                    else if (element.Arguments?.Count > 0)
                    {
                        var commandArgument = element.Arguments[currentCommandArgumentPosition++];
                        AddArgumentContext(ref argumentContexts, commandArgument, nextPosition, nextPosition + 1, arguments);
                        if (commandArgument.IsVariadic)
                            currentCommandArgumentPosition--;
                    }
                    nextPosition++;
                }
            }
        }
        return new CommandTreeElementContext(element, position, child)
        {
            HasHelpOption = hasHelpOption,
            Options = optionContexts,
            Arguments = argumentContexts
        };
    }

    public static CommandTreeContext BuildAndLoad(ICommandLineBuilder commandLineBuilder, string[] arguments)
    {
        var commandLineBuilderInternals = (ICommandLineBuilderInternals)commandLineBuilder;
        var commandTreeElement = BuildElement(commandLineBuilder);
        var element = BuildAndLoad(commandTreeElement, commandLineBuilder, -1, arguments);
        return new()
        {
            Arguments = arguments,
            Root = element,
            Tree = new CommandTree
            {
                Root = commandTreeElement,
                CommandLineOptions = commandLineBuilderInternals.CommandLineOptions
            }
        };
    }

    public static CommandTree Build(ICommandLineBuilder commandLineBuilder)
    {
        var commandLineBuilderInternals = (ICommandLineBuilderInternals)commandLineBuilder;
        var element = BuildElement(commandLineBuilder);
        BuildChildren(element, commandLineBuilder);
        return new()
        {
            Root = element,
            CommandLineOptions = commandLineBuilderInternals.CommandLineOptions
        };
    }

    public static CommandTreeContext Load(CommandTree commandTree, string[] arguments)
    {
        var element = Load(commandTree.CommandLineOptions, commandTree.Root, -1, arguments);
        return new()
        {
            Arguments = arguments,
            Root = element,
            Tree = commandTree
        };
    }
}
