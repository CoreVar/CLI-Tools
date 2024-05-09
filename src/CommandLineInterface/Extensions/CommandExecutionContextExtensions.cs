using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Runtime;
using CoreVar.CommandLineInterface.Support;
using CoreVar.CommandLineInterface.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

public static partial class BuilderExtensions
{

    
    public static T GetOption<T>(this CommandExecutionContext context, ICommandOptionBuilder<T> optionBuilder)
    {
        var optionBuilderInternals = (ICommandOptionBuilderInternals)optionBuilder;
        var commandTreeContext = context.Services.GetRequiredService<CommandTreeContext>();
        var executingCommand = CommandTreeHelpers.GetExecutingCommand(commandTreeContext.Root);
        if (executingCommand.Options?.TryGetValue(optionBuilderInternals.Name, out var option) == true)
        {
            if (!option.Option.GetValueHandler(context, option, out var value))
            {
                // TODO: Report error
                return default!;
            }
            return (T)value;
        }
        return default!;
    }

    public static ValueTuple<T1> GetOptions<T1>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>> optionBuilders)
        => new(context.GetOption(optionBuilders.Item1));

    public static ValueTuple<T1, T2> GetOptions<T1, T2>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>, ICommandOptionBuilder<T2>> optionBuilders)
        => new(
            context.GetOption(optionBuilders.Item1), 
            context.GetOption(optionBuilders.Item2));

    public static ValueTuple<T1, T2, T3> GetOptions<T1, T2, T3>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>, ICommandOptionBuilder<T2>, ICommandOptionBuilder<T3>> optionBuilders)
        => new(
            context.GetOption(optionBuilders.Item1), 
            context.GetOption(optionBuilders.Item2), 
            context.GetOption(optionBuilders.Item3));

    public static ValueTuple<T1, T2, T3, T4> GetOptions<T1, T2, T3, T4>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>, ICommandOptionBuilder<T2>, ICommandOptionBuilder<T3>, ICommandOptionBuilder<T4>> optionBuilders)
        => new(
            context.GetOption(optionBuilders.Item1),
            context.GetOption(optionBuilders.Item2),
            context.GetOption(optionBuilders.Item3),
            context.GetOption(optionBuilders.Item4));


    public static ValueTuple<T1, T2, T3, T4, T5> GetOptions<T1, T2, T3, T4, T5>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>, ICommandOptionBuilder<T2>, ICommandOptionBuilder<T3>, ICommandOptionBuilder<T4>, ICommandOptionBuilder<T5>> optionBuilders)
        => new(
            context.GetOption(optionBuilders.Item1),
            context.GetOption(optionBuilders.Item2),
            context.GetOption(optionBuilders.Item3),
            context.GetOption(optionBuilders.Item4),
            context.GetOption(optionBuilders.Item5));

    public static ValueTuple<T1, T2, T3, T4, T5, T6> GetOptions<T1, T2, T3, T4, T5, T6>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>, ICommandOptionBuilder<T2>, ICommandOptionBuilder<T3>, ICommandOptionBuilder<T4>, ICommandOptionBuilder<T5>, ICommandOptionBuilder<T6>> optionBuilders)
        => new(
            context.GetOption(optionBuilders.Item1),
            context.GetOption(optionBuilders.Item2),
            context.GetOption(optionBuilders.Item3),
            context.GetOption(optionBuilders.Item4),
            context.GetOption(optionBuilders.Item5),
            context.GetOption(optionBuilders.Item6));

    public static ValueTuple<T1, T2, T3, T4, T5, T6, T7> GetOptions<T1, T2, T3, T4, T5, T6, T7>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>, ICommandOptionBuilder<T2>, ICommandOptionBuilder<T3>, ICommandOptionBuilder<T4>, ICommandOptionBuilder<T5>, ICommandOptionBuilder<T6>, ICommandOptionBuilder<T7>> optionBuilders)
        => new(
            context.GetOption(optionBuilders.Item1),
            context.GetOption(optionBuilders.Item2),
            context.GetOption(optionBuilders.Item3),
            context.GetOption(optionBuilders.Item4),
            context.GetOption(optionBuilders.Item5),
            context.GetOption(optionBuilders.Item6),
            context.GetOption(optionBuilders.Item7));


    private static object GetStringArgument(CommandExecutionContext context, CommandTreeArgumentContext argument)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException("The string value must be a length of 1.");
        return context.Arguments[argument.ValueRange.Start.Value];
    }

    private static object GetBooleanArgument(CommandExecutionContext context, CommandTreeArgumentContext argument)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException("The boolean value must be a length of 1.");
        return bool.Parse(context.Arguments[argument.ValueRange.Start.Value]);
    }

    public static T GetArgument<T>(this CommandExecutionContext context, ICommandArgumentBuilder<T> argumentBuilder)
    {
        var argumentBuilderInternals = (ICommandArgumentBuilderInternals)argumentBuilder;
        var commandTreeContext = context.Services.GetRequiredService<CommandTreeContext>();
        var executingCommand = CommandTreeHelpers.GetExecutingCommand(commandTreeContext.Root);
        if (executingCommand.Arguments?.TryGetValue(argumentBuilderInternals.Name, out var argument) == true)
        {
            if (!argument.Argument.GetValueHandler(context, argument, out var value))
            {
                // TODO: Report error
                return default!;
            }
            return (T)value;
        }
        return default!;
    }

    public static ValueTuple<T1> GetArguments<T1>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>> argumentBuilders)
        => new(context.GetArgument(argumentBuilders.Item1));

    public static ValueTuple<T1, T2> GetArguments<T1, T2>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>, ICommandArgumentBuilder<T2>> argumentBuilders)
        => new(
            context.GetArgument(argumentBuilders.Item1),
            context.GetArgument(argumentBuilders.Item2));

    public static ValueTuple<T1, T2, T3> GetArguments<T1, T2, T3>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>, ICommandArgumentBuilder<T2>, ICommandArgumentBuilder<T3>> argumentBuilders)
        => new(
            context.GetArgument(argumentBuilders.Item1),
            context.GetArgument(argumentBuilders.Item2),
            context.GetArgument(argumentBuilders.Item3));

    public static ValueTuple<T1, T2, T3, T4> GetArguments<T1, T2, T3, T4>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>, ICommandArgumentBuilder<T2>, ICommandArgumentBuilder<T3>, ICommandArgumentBuilder<T4>> argumentBuilders)
        => new(
            context.GetArgument(argumentBuilders.Item1),
            context.GetArgument(argumentBuilders.Item2),
            context.GetArgument(argumentBuilders.Item3),
            context.GetArgument(argumentBuilders.Item4));


    public static ValueTuple<T1, T2, T3, T4, T5> GetArguments<T1, T2, T3, T4, T5>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>, ICommandArgumentBuilder<T2>, ICommandArgumentBuilder<T3>, ICommandArgumentBuilder<T4>, ICommandArgumentBuilder<T5>> argumentBuilders)
        => new(
            context.GetArgument(argumentBuilders.Item1),
            context.GetArgument(argumentBuilders.Item2),
            context.GetArgument(argumentBuilders.Item3),
            context.GetArgument(argumentBuilders.Item4),
            context.GetArgument(argumentBuilders.Item5));

    public static ValueTuple<T1, T2, T3, T4, T5, T6> GetArguments<T1, T2, T3, T4, T5, T6>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>, ICommandArgumentBuilder<T2>, ICommandArgumentBuilder<T3>, ICommandArgumentBuilder<T4>, ICommandArgumentBuilder<T5>, ICommandArgumentBuilder<T6>> argumentBuilders)
        => new(
            context.GetArgument(argumentBuilders.Item1),
            context.GetArgument(argumentBuilders.Item2),
            context.GetArgument(argumentBuilders.Item3),
            context.GetArgument(argumentBuilders.Item4),
            context.GetArgument(argumentBuilders.Item5),
            context.GetArgument(argumentBuilders.Item6));

    public static ValueTuple<T1, T2, T3, T4, T5, T6, T7> GetArguments<T1, T2, T3, T4, T5, T6, T7>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>, ICommandArgumentBuilder<T2>, ICommandArgumentBuilder<T3>, ICommandArgumentBuilder<T4>, ICommandArgumentBuilder<T5>, ICommandArgumentBuilder<T6>, ICommandArgumentBuilder<T7>> argumentBuilders)
        => new(
            context.GetArgument(argumentBuilders.Item1),
            context.GetArgument(argumentBuilders.Item2),
            context.GetArgument(argumentBuilders.Item3),
            context.GetArgument(argumentBuilders.Item4),
            context.GetArgument(argumentBuilders.Item5),
            context.GetArgument(argumentBuilders.Item6),
            context.GetArgument(argumentBuilders.Item7));

}
