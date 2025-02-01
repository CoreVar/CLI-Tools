using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Runtime;
using System.Xml.Linq;

namespace CoreVar.CommandLineInterface.Support;

public class CommandTreeBuilder
{

    private static CommandTreeElement BuildElement(IParentBuilder builder)
    {
        var parentBuilderInternals = (IParentBuilderInternals)builder;
        var executableBuilderInternals = (IExecutableBuilderInternals)builder;

        var element = new CommandTreeElement(parentBuilderInternals.CommandLineOptions, parentBuilderInternals.Name)
        {
            Description = parentBuilderInternals.Description,
            ExecuteDelegate = executableBuilderInternals.ExecuteDelegate,
            Usages = executableBuilderInternals.Usages,

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
            Aliases = builderInternals.Aliases,
            HostBuilders = builderInternals.HostBuilders,
            HostSetups = builderInternals.HostSetups,
            Description = builderInternals.Description
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
            HostBuilders = builderInternals.HostBuilders,
            HostSetups = builderInternals.HostSetups,
            Description = builderInternals.Description
        };

        return argument;
    }

    private static (CommandTreeElement Element, IParentBuilder Builder) BuildChild(CommandTreeElement parent, IParentBuilder parentBuilder, string commandName, Action<ICommandBuilder> builder)
    {
        var parentBuilderInternals = (IParentBuilderInternals)parentBuilder;
        var commandBuilder = new CommandBuilder(commandName, parentBuilderInternals.CommandLineOptions);
        builder(commandBuilder);
        var childElement = BuildElement(commandBuilder);
        parent.Children.Add(commandName, childElement);
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
                while (nextPosition < arguments.Length)
                {
                    argument = arguments[nextPosition];

                    if (!element.DisableHelp)
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


                    if (element.OptionAliases is not null && element.OptionAliases.TryGetValue(argument, out var commandOptionName) == true)
                        argument = commandOptionName;

                    if (element.Options?.TryGetValue(argument, out var commandOption) == true)
                    {
                        optionContexts ??= [];
                        optionContexts.Add(commandOption.Name, new CommandTreeOptionContext
                        {
                            Option = commandOption,
                            Position = nextPosition,
                            ValueLength = commandOption.AcceptsValue ? 1 : 0
                        });
                    }
                    else if (element.Arguments?.Count > currentArgumentPosition)
                    {
                        argumentContexts ??= [];
                        var commandArgument = element.Arguments[currentArgumentPosition++];
                        argumentContexts.Add(commandArgument.Name, new CommandTreeArgumentContext
                        {
                            Argument = commandArgument,
                            ValueRange = new Range(nextPosition, nextPosition + 1)
                        });
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
                BuildCommandElements(element, executableBuilder);

                int currentCommandArgumentPosition = 0;
                while (nextPosition < arguments.Length)
                {
                    commandKey = arguments[nextPosition];

                    if (!(element.DisableHelp = executableBuilderInternals.DisableHelp))
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

                    if (element.HelpOptionComparer is not null)
                    {
                        if (element.HelpOptionComparer(arguments[nextPosition]))
                            hasHelpOption = true;
                    }
                    else
                    {
                        if (parentBuilderInternals.CommandLineOptions.OptionComparer.Equals("--help", arguments[nextPosition]) ||
                            parentBuilderInternals.CommandLineOptions.OptionComparer.Equals("-?", arguments[nextPosition]) ||
                            parentBuilderInternals.CommandLineOptions.OptionComparer.Equals("-h", arguments[nextPosition]))
                            hasHelpOption = true;
                    }

                    if (element.OptionAliases is not null && element.OptionAliases.TryGetValue(commandKey, out var commandOptionName) == true)
                        commandKey = commandOptionName;

                    if (element.Options?.TryGetValue(commandKey, out var commandOption) == true)
                    {
                        optionContexts ??= [];
                        optionContexts.Add(commandOption.Name, new CommandTreeOptionContext
                        {
                            Option = commandOption,
                            Position = nextPosition,
                            ValueLength = commandOption.AcceptsValue ? 1 : 0
                        });

                        if (commandOption.AcceptsValue)
                            nextPosition++;
                    }
                    else if (element.Arguments?.Count > 0)
                    {
                        argumentContexts ??= [];
                        var commandArgument = element.Arguments[currentCommandArgumentPosition++];
                        argumentContexts.Add(commandArgument.Name, new CommandTreeArgumentContext
                        {
                            Argument = commandArgument,
                            ValueRange = new Range(nextPosition, nextPosition + 1)
                        });
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
