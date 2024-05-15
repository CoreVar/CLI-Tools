using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Runtime;
using CoreVar.CommandLineInterface.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace CoreVar.CommandLineInterface;

public static partial class BuilderExtensions
{
    
    /// <summary>
    /// Gets the option value for the executing command.
    /// </summary>
    /// <typeparam name="T">The option type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="optionBuilder">The option builder.</param>
    /// <returns>The option value.</returns>
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

    /// <summary>
    /// Gets the option values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first option type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="optionBuilders">The option builder.</param>
    /// <returns>The option values.</returns>
    public static ValueTuple<T1> GetOptions<T1>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>> optionBuilders)
        => new(context.GetOption(optionBuilders.Item1));

    /// <summary>
    /// Gets the option values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first option type.</typeparam>
    /// <typeparam name="T2">The second option type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="optionBuilders">The option builder.</param>
    /// <returns>The option values.</returns>
    public static ValueTuple<T1, T2> GetOptions<T1, T2>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>, ICommandOptionBuilder<T2>> optionBuilders)
        => new(
            context.GetOption(optionBuilders.Item1), 
            context.GetOption(optionBuilders.Item2));

    /// <summary>
    /// Gets the option values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first option type.</typeparam>
    /// <typeparam name="T2">The second option type.</typeparam>
    /// <typeparam name="T3">The third option type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="optionBuilders">The option builder.</param>
    /// <returns>The option values.</returns>
    public static ValueTuple<T1, T2, T3> GetOptions<T1, T2, T3>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>, ICommandOptionBuilder<T2>, ICommandOptionBuilder<T3>> optionBuilders)
        => new(
            context.GetOption(optionBuilders.Item1), 
            context.GetOption(optionBuilders.Item2), 
            context.GetOption(optionBuilders.Item3));

    /// <summary>
    /// Gets the option values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first option type.</typeparam>
    /// <typeparam name="T2">The second option type.</typeparam>
    /// <typeparam name="T3">The third option type.</typeparam>
    /// <typeparam name="T4">The fourth option type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="optionBuilders">The option builder.</param>
    /// <returns>The option values.</returns>
    public static ValueTuple<T1, T2, T3, T4> GetOptions<T1, T2, T3, T4>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>, ICommandOptionBuilder<T2>, ICommandOptionBuilder<T3>, ICommandOptionBuilder<T4>> optionBuilders)
        => new(
            context.GetOption(optionBuilders.Item1),
            context.GetOption(optionBuilders.Item2),
            context.GetOption(optionBuilders.Item3),
            context.GetOption(optionBuilders.Item4));

    /// <summary>
    /// Gets the option values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first option type.</typeparam>
    /// <typeparam name="T2">The second option type.</typeparam>
    /// <typeparam name="T3">The third option type.</typeparam>
    /// <typeparam name="T4">The fourth option type.</typeparam>
    /// <typeparam name="T5">The fifth option type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="optionBuilders">The option builder.</param>
    /// <returns>The option values.</returns>
    public static ValueTuple<T1, T2, T3, T4, T5> GetOptions<T1, T2, T3, T4, T5>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>, ICommandOptionBuilder<T2>, ICommandOptionBuilder<T3>, ICommandOptionBuilder<T4>, ICommandOptionBuilder<T5>> optionBuilders)
        => new(
            context.GetOption(optionBuilders.Item1),
            context.GetOption(optionBuilders.Item2),
            context.GetOption(optionBuilders.Item3),
            context.GetOption(optionBuilders.Item4),
            context.GetOption(optionBuilders.Item5));

    /// <summary>
    /// Gets the option values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first option type.</typeparam>
    /// <typeparam name="T2">The second option type.</typeparam>
    /// <typeparam name="T3">The third option type.</typeparam>
    /// <typeparam name="T4">The fourth option type.</typeparam>
    /// <typeparam name="T5">The fifth option type.</typeparam>
    /// <typeparam name="T6">The sixth option type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="optionBuilders">The option builder.</param>
    /// <returns>The option values.</returns>
    public static ValueTuple<T1, T2, T3, T4, T5, T6> GetOptions<T1, T2, T3, T4, T5, T6>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>, ICommandOptionBuilder<T2>, ICommandOptionBuilder<T3>, ICommandOptionBuilder<T4>, ICommandOptionBuilder<T5>, ICommandOptionBuilder<T6>> optionBuilders)
        => new(
            context.GetOption(optionBuilders.Item1),
            context.GetOption(optionBuilders.Item2),
            context.GetOption(optionBuilders.Item3),
            context.GetOption(optionBuilders.Item4),
            context.GetOption(optionBuilders.Item5),
            context.GetOption(optionBuilders.Item6));

    /// <summary>
    /// Gets the option values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first option type.</typeparam>
    /// <typeparam name="T2">The second option type.</typeparam>
    /// <typeparam name="T3">The third option type.</typeparam>
    /// <typeparam name="T4">The fourth option type.</typeparam>
    /// <typeparam name="T5">The fifth option type.</typeparam>
    /// <typeparam name="T6">The sixth option type.</typeparam>
    /// <typeparam name="T7">The seventh option type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="optionBuilders">The option builder.</param>
    /// <returns>The option values.</returns>
    public static ValueTuple<T1, T2, T3, T4, T5, T6, T7> GetOptions<T1, T2, T3, T4, T5, T6, T7>(this CommandExecutionContext context, ValueTuple<ICommandOptionBuilder<T1>, ICommandOptionBuilder<T2>, ICommandOptionBuilder<T3>, ICommandOptionBuilder<T4>, ICommandOptionBuilder<T5>, ICommandOptionBuilder<T6>, ICommandOptionBuilder<T7>> optionBuilders)
        => new(
            context.GetOption(optionBuilders.Item1),
            context.GetOption(optionBuilders.Item2),
            context.GetOption(optionBuilders.Item3),
            context.GetOption(optionBuilders.Item4),
            context.GetOption(optionBuilders.Item5),
            context.GetOption(optionBuilders.Item6),
            context.GetOption(optionBuilders.Item7));

    /// <summary>
    /// Gets the argument value for the executing command.
    /// </summary>
    /// <typeparam name="T">The argument type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="argumentBuilder">The argument builder.</param>
    /// <returns>The argument value.</returns>
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

    /// <summary>
    /// Gets the argument values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first argument type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="argumentBuilders">The argument builders.</param>
    /// <returns>The argument values.</returns>
    public static ValueTuple<T1> GetArguments<T1>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>> argumentBuilders)
        => new(context.GetArgument(argumentBuilders.Item1));

    /// <summary>
    /// Gets the argument values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first argument type.</typeparam>
    /// <typeparam name="T2">The second argument type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="argumentBuilders">The argument builders.</param>
    /// <returns>The argument values.</returns>
    public static ValueTuple<T1, T2> GetArguments<T1, T2>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>, ICommandArgumentBuilder<T2>> argumentBuilders)
        => new(
            context.GetArgument(argumentBuilders.Item1),
            context.GetArgument(argumentBuilders.Item2));

    /// <summary>
    /// Gets the argument values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first argument type.</typeparam>
    /// <typeparam name="T2">The second argument type.</typeparam>
    /// <typeparam name="T3">The third argument type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="argumentBuilders">The argument builders.</param>
    /// <returns>The argument values.</returns>
    public static ValueTuple<T1, T2, T3> GetArguments<T1, T2, T3>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>, ICommandArgumentBuilder<T2>, ICommandArgumentBuilder<T3>> argumentBuilders)
        => new(
            context.GetArgument(argumentBuilders.Item1),
            context.GetArgument(argumentBuilders.Item2),
            context.GetArgument(argumentBuilders.Item3));

    /// <summary>
    /// Gets the argument values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first argument type.</typeparam>
    /// <typeparam name="T2">The second argument type.</typeparam>
    /// <typeparam name="T3">The third argument type.</typeparam>
    /// <typeparam name="T4">The fourth argument type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="argumentBuilders">The argument builders.</param>
    /// <returns>The argument values.</returns>
    public static ValueTuple<T1, T2, T3, T4> GetArguments<T1, T2, T3, T4>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>, ICommandArgumentBuilder<T2>, ICommandArgumentBuilder<T3>, ICommandArgumentBuilder<T4>> argumentBuilders)
        => new(
            context.GetArgument(argumentBuilders.Item1),
            context.GetArgument(argumentBuilders.Item2),
            context.GetArgument(argumentBuilders.Item3),
            context.GetArgument(argumentBuilders.Item4));

    /// <summary>
    /// Gets the argument values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first argument type.</typeparam>
    /// <typeparam name="T2">The second argument type.</typeparam>
    /// <typeparam name="T3">The third argument type.</typeparam>
    /// <typeparam name="T4">The fourth argument type.</typeparam>
    /// <typeparam name="T5">The fifth argument type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="argumentBuilders">The argument builders.</param>
    /// <returns>The argument values.</returns>
    public static ValueTuple<T1, T2, T3, T4, T5> GetArguments<T1, T2, T3, T4, T5>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>, ICommandArgumentBuilder<T2>, ICommandArgumentBuilder<T3>, ICommandArgumentBuilder<T4>, ICommandArgumentBuilder<T5>> argumentBuilders)
        => new(
            context.GetArgument(argumentBuilders.Item1),
            context.GetArgument(argumentBuilders.Item2),
            context.GetArgument(argumentBuilders.Item3),
            context.GetArgument(argumentBuilders.Item4),
            context.GetArgument(argumentBuilders.Item5));

    /// <summary>
    /// Gets the argument values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first argument type.</typeparam>
    /// <typeparam name="T2">The second argument type.</typeparam>
    /// <typeparam name="T3">The third argument type.</typeparam>
    /// <typeparam name="T4">The fourth argument type.</typeparam>
    /// <typeparam name="T5">The fifth argument type.</typeparam>
    /// <typeparam name="T6">The sixth argument type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="argumentBuilders">The argument builders.</param>
    /// <returns>The argument values.</returns>
    public static ValueTuple<T1, T2, T3, T4, T5, T6> GetArguments<T1, T2, T3, T4, T5, T6>(this CommandExecutionContext context, ValueTuple<ICommandArgumentBuilder<T1>, ICommandArgumentBuilder<T2>, ICommandArgumentBuilder<T3>, ICommandArgumentBuilder<T4>, ICommandArgumentBuilder<T5>, ICommandArgumentBuilder<T6>> argumentBuilders)
        => new(
            context.GetArgument(argumentBuilders.Item1),
            context.GetArgument(argumentBuilders.Item2),
            context.GetArgument(argumentBuilders.Item3),
            context.GetArgument(argumentBuilders.Item4),
            context.GetArgument(argumentBuilders.Item5),
            context.GetArgument(argumentBuilders.Item6));

    /// <summary>
    /// Gets the argument values for the executing command.
    /// </summary>
    /// <typeparam name="T1">The first argument type.</typeparam>
    /// <typeparam name="T2">The second argument type.</typeparam>
    /// <typeparam name="T3">The third argument type.</typeparam>
    /// <typeparam name="T4">The fourth argument type.</typeparam>
    /// <typeparam name="T5">The fifth argument type.</typeparam>
    /// <typeparam name="T6">The sixth argument type.</typeparam>
    /// <typeparam name="T7">The seventh argument type.</typeparam>
    /// <param name="context">The <see cref="CommandExecutionContext"/> for the currently executing command.</param>
    /// <param name="argumentBuilders">The argument builders.</param>
    /// <returns>The argument values.</returns>
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
