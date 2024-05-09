using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Support;
using CoreVar.CommandLineInterface.Utilities;
using Microsoft.Extensions.Hosting;

namespace CoreVar.CommandLineInterface;

public static class GenericExtensions
{

    public static T SetupHostBuilder<T>(this T source, Action<IHostApplicationBuilder> builderSetup) where T : IBuilder
    {
        ((IBuilderInternals)source).AddHostBuilder(builderSetup);

        return source;
    }

    public static T SetupHost<T>(this T source, Action<IHost> hostSetup) where T : IBuilder
    {
        ((IBuilderInternals)source).AddHostSetup(hostSetup);

        return source;
    }

    public static T Description<T>(this T source, string description) where T : IBuilder
    {
        ((IBuilderInternals)source).Description = description;

        return source;
    }

    public static T Command<T>(this T source, string name, Action<ICommandBuilder> builder) where T : IParentBuilder
    {
        ((IParentBuilderInternals)source).Children.Add(name, builder);

        return source;
    }

    public static T OnExecute<T>(this T source, Func<CommandExecutionContext, ValueTask> handler) where T : IExecutableBuilder
    {
        var sourceInternals = (IExecutableBuilderInternals)source;
        if (sourceInternals.ExecuteDelegate is not null)
            throw new InvalidOperationException("An execute handler has already been assigned.");

        sourceInternals.ExecuteDelegate = handler;

        return source;
    }

    public static T OnExecute<T>(this T source, Action<CommandExecutionContext> handler) where T : IExecutableBuilder
        => OnExecute(source, context =>
        {
            handler(context);

            return ValueTask.CompletedTask;
        });

    public static T OnExecute<T>(this T source, Func<ValueTask> handler) where T : IExecutableBuilder
        => OnExecute(source, async _ => await handler());

    public static T OnExecute<T>(this T source, Action handler) where T : IExecutableBuilder
        => OnExecute(source, _ => handler());

    public static T OnExecute<T>(this T source, Func<CommandExecutionContext, ValueTask<int>> handler) where T : IExecutableBuilder
    {
        OnExecute(source, async context =>
        {
            var result = await handler(context);
            context.Result = result;
        });

        return source;
    }

    public static T OnExecute<T>(this T source, Func<CommandExecutionContext, int> handler) where T : IExecutableBuilder
        => OnExecute(source, context =>
        {
            var result = handler(context);
            context.Result = result;

            return ValueTask.CompletedTask;
        });

    public static T OnExecute<T>(this T source, Func<ValueTask<int>> handler) where T : IExecutableBuilder
    {
        OnExecute(source, async _ => await handler());

        return source;
    }

    public static T OnExecute<T>(this T source, Func<int> handler) where T : IExecutableBuilder
        => OnExecute(source, _ => handler());

    public static T Usage<T>(this T source, Func<IServiceProvider, string> usage) where T : IExecutableBuilder
    {
        var builderInternals = (IExecutableBuilderInternals)source;
        builderInternals.Usages ??= [];
        builderInternals.Usages.Add(new Runtime.CommandTreeElementUsage
        {
            Usage = usage
        });
        return source;
    }

    public static T Usage<T>(this T source, Func<IServiceProvider, string> usage, string description) where T : IExecutableBuilder
    {
        var builderInternals = (IExecutableBuilderInternals)source;
        builderInternals.Usages ??= [];
        builderInternals.Usages.Add(new Runtime.CommandTreeElementUsage
        { 
            Usage = usage,
            Description = description
        });
        return source;
    }

    public static ICommandOptionBuilder<T> Option<T>(this IExecutableBuilder source, string name)
    {
        var executableBuilderInternals = (IExecutableBuilderInternals)source;
        var builderInternals = (IBuilderInternals)source;
        var commandOptionBuilder = new CommandOptionBuilder<T>(name, builderInternals.CommandLineOptions);
        var commandOptionBuilderInternals = (ICommandOptionBuilderInternals)commandOptionBuilder;

        executableBuilderInternals.Options.Add(name, commandOptionBuilder);

        var type = typeof(T);

        if (type == typeof(bool))
            commandOptionBuilderInternals.AcceptsValue = false;
        else
            commandOptionBuilderInternals.AcceptsValue = true;


        if ((type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)))
            commandOptionBuilderInternals.IsRequired = false;
        else
            commandOptionBuilderInternals.IsRequired = true;

        if (typeof(T) == typeof(string))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetStringOption;
        else if (typeof(T) == typeof(bool))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetBooleanOption;
        else if (typeof(T) == typeof(byte))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetByteOption;
        else if (typeof(T) == typeof(ushort))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetUInt16Option;
        else if (typeof(T) == typeof(uint))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetUInt32Option;
        else if (typeof(T) == typeof(ulong))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetUInt64Option;
        else if (typeof(T) == typeof(sbyte))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetSByteOption;
        else if (typeof(T) == typeof(short))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetInt16Option;
        else if (typeof(T) == typeof(int))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetInt32Option;
        else if (typeof(T) == typeof(long))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetInt64Option;
        else if (typeof(T) == typeof(float))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetSingleOption;
        else if (typeof(T) == typeof(double))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetDoubleOption;
        else if (typeof(T) == typeof(decimal))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetDecimalOption;
        else if (typeof(T) == typeof(Guid))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetGuidOption;
        else if (typeof(T) == typeof(DateTime))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetDateTimeOption;
        else if (typeof(T) == typeof(DateTimeOffset))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetDateTimeOffsetOption;
        else if (typeof(T) == typeof(byte[]))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetByteArrayOption;

        else if (typeof(T) == typeof(bool?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetBooleanOption;
        else if (typeof(T) == typeof(byte?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetByteOption;
        else if (typeof(T) == typeof(ushort?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetUInt16Option;
        else if (typeof(T) == typeof(uint?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetUInt32Option;
        else if (typeof(T) == typeof(ulong?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetUInt64Option;
        else if (typeof(T) == typeof(sbyte?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetSByteOption;
        else if (typeof(T) == typeof(short?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetInt16Option;
        else if (typeof(T) == typeof(int?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetInt32Option;
        else if (typeof(T) == typeof(long?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetInt64Option;
        else if (typeof(T) == typeof(float?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetSingleOption;
        else if (typeof(T) == typeof(double?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetDoubleOption;
        else if (typeof(T) == typeof(decimal?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetDecimalOption;
        else if (typeof(T) == typeof(Guid?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetGuidOption;
        else if (typeof(T) == typeof(DateTime?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetDateTimeOption;
        else if (typeof(T) == typeof(DateTimeOffset?))
            commandOptionBuilderInternals.GetValueHandler = BuilderUtilities.TryGetDateTimeOffsetOption;

        return commandOptionBuilder;
    }

    public static ICommandOptionBuilder<bool> Option(this IExecutableBuilder source, string name)
    {
        return source.Option<bool>(name).IsOptional();
    }

    public static ICommandArgumentBuilder<T> Argument<T>(this IExecutableBuilder source, string name)
    {
        var executableBuilderInternals = (IExecutableBuilderInternals)source;
        var builderInternals = (IBuilderInternals)source;
        var commandArgumentBuilder = new CommandArgumentBuilder<T>(name, builderInternals.CommandLineOptions);
        var commandArgumentBuilderInternals = (ICommandArgumentBuilderInternals)commandArgumentBuilder;

        var type = typeof(T);

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
            commandArgumentBuilderInternals.IsRequired = false;
        else
            commandArgumentBuilderInternals.IsRequired = true;

        if (typeof(T) == typeof(string))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetStringArgument;
        else if (typeof(T) == typeof(bool))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetBooleanArgument;
        else if (typeof(T) == typeof(byte))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetByteArgument;
        else if (typeof(T) == typeof(ushort))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetUInt16Argument;
        else if (typeof(T) == typeof(uint))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetUInt32Argument;
        else if (typeof(T) == typeof(ulong))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetUInt64Argument;
        else if (typeof(T) == typeof(sbyte))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetSByteArgument;
        else if (typeof(T) == typeof(short))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetInt16Argument;
        else if (typeof(T) == typeof(int))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetInt32Argument;
        else if (typeof(T) == typeof(long))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetInt64Argument;
        else if (typeof(T) == typeof(float))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetSingleArgument;
        else if (typeof(T) == typeof(double))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetDoubleArgument;
        else if (typeof(T) == typeof(decimal))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetDecimalArgument;
        else if (typeof(T) == typeof(Guid))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetGuidArgument;
        else if (typeof(T) == typeof(DateTime))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetDateTimeArgument;
        else if (typeof(T) == typeof(DateTimeOffset))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetDateTimeOffsetArgument;
        else if (typeof(T) == typeof(byte[]))
            commandArgumentBuilderInternals.GetValueHandler = BuilderUtilities.TryGetByteArrayArgument;

        executableBuilderInternals.Arguments.Add(commandArgumentBuilder);

        return commandArgumentBuilder;
    }

    public static IHelpOptionBuilder HelpOption<T>(this T source, string option) where T : IExecutableBuilder
    {
        var builderInternals = (IExecutableBuilderInternals)source;
        var helpOptionBuilder = new HelpOptionBuilder(builderInternals.CommandLineOptions);
        var helpOptionBuilderInternals = (IHelpOptionBuilderInternals)helpOptionBuilder;
        helpOptionBuilderInternals.Option = option;

        builderInternals.HelpOption = helpOptionBuilder;

        return helpOptionBuilder;
    }

    public static T WithoutHelp<T>(this T source) where T : IExecutableBuilder
    {
        var builderInternals = (IExecutableBuilderInternals)source;
        builderInternals.DisableHelp = true;
        return source;
    }

}