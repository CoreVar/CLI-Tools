using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;

namespace CoreVar.CommandLineInterface;

/// <summary>Configures defaults, validation, completion, and alternate value sources.</summary>
public static class ValueBuilderExtensions
{
    public static ICommandOptionBuilder<T> Default<T>(this ICommandOptionBuilder<T> builder, T value)
    {
        var metadata = (ICommandOptionBuilderInternals)builder;
        metadata.DefaultValue = value;
        metadata.IsRequired = false;
        return builder;
    }

    public static ICommandOptionBuilder<T> FromEnvironment<T>(this ICommandOptionBuilder<T> builder, string variable)
    {
        var metadata = (ICommandOptionBuilderInternals)builder;
        metadata.EnvironmentVariable = variable;
        metadata.IsRequired = false;
        return builder;
    }

    /// <summary>Binds from an environment variable derived from the app prefix and option name.</summary>
    public static ICommandOptionBuilder<T> FromEnvironment<T>(this ICommandOptionBuilder<T> builder)
    {
        var metadata = (ICommandOptionBuilderInternals)builder;
        var name = metadata.Name.TrimStart('-').Replace('-', '_').ToUpperInvariant();
        return builder.FromEnvironment($"{metadata.CommandLineOptions.EnvironmentPrefix}{name}");
    }

    public static ICommandOptionBuilder<T> FromConfiguration<T>(this ICommandOptionBuilder<T> builder, string key)
    {
        var metadata = (ICommandOptionBuilderInternals)builder;
        metadata.ConfigurationKey = key;
        metadata.IsRequired = false;
        return builder;
    }

    /// <summary>Binds from a configuration key derived from the option name.</summary>
    public static ICommandOptionBuilder<T> FromConfiguration<T>(this ICommandOptionBuilder<T> builder)
        => builder.FromConfiguration(((ICommandOptionBuilderInternals)builder).Name.TrimStart('-').Replace('-', ':'));

    public static ICommandOptionBuilder<T> Global<T>(this ICommandOptionBuilder<T> builder, bool global = true)
    {
        ((ICommandOptionBuilderInternals)builder).IsGlobal = global;
        return builder;
    }

    public static ICommandOptionBuilder<T> Hidden<T>(this ICommandOptionBuilder<T> builder, bool hidden = true)
    {
        ((ICommandOptionBuilderInternals)builder).IsHidden = hidden;
        return builder;
    }

    public static ICommandOptionBuilder<T> Deprecated<T>(this ICommandOptionBuilder<T> builder, string message)
    {
        ((ICommandOptionBuilderInternals)builder).DeprecationMessage = message;
        return builder;
    }

    public static ICommandOptionBuilder<T> Complete<T>(this ICommandOptionBuilder<T> builder, params string[] values)
    {
        ((ICommandOptionBuilderInternals)builder).Completions.AddRange(values);
        return builder;
    }

    public static ICommandOptionBuilder<T> Must<T>(this ICommandOptionBuilder<T> builder, Func<T, bool> predicate, string message)
    {
        ((ICommandOptionBuilderInternals)builder).Validators.Add(value => value is T typed && !predicate(typed) ? message : null);
        return builder;
    }

    public static ICommandOptionBuilder<T> Range<T>(this ICommandOptionBuilder<T> builder, T minimum, T maximum)
        where T : IComparable<T>
        => builder.Must(value => value.CompareTo(minimum) >= 0 && value.CompareTo(maximum) <= 0,
            $"Value must be between {minimum} and {maximum}.");

    public static ICommandArgumentBuilder<T> Default<T>(this ICommandArgumentBuilder<T> builder, T value)
    {
        var metadata = (ICommandArgumentBuilderInternals)builder;
        metadata.DefaultValue = value;
        metadata.IsRequired = false;
        return builder;
    }

    public static ICommandArgumentBuilder<T> Variadic<T>(this ICommandArgumentBuilder<T> builder, bool variadic = true)
    {
        ((ICommandArgumentBuilderInternals)builder).IsVariadic = variadic;
        return builder;
    }

    public static ICommandArgumentBuilder<T> Complete<T>(this ICommandArgumentBuilder<T> builder, params string[] values)
    {
        ((ICommandArgumentBuilderInternals)builder).Completions.AddRange(values);
        return builder;
    }

    public static ICommandArgumentBuilder<T> Must<T>(this ICommandArgumentBuilder<T> builder, Func<T, bool> predicate, string message)
    {
        ((ICommandArgumentBuilderInternals)builder).Validators.Add(value => value is T typed && !predicate(typed) ? message : null);
        return builder;
    }

    public static ICommandOptionBuilder<T> GlobalOption<T>(this ICommandLineBuilder builder, string name)
        => builder.Option<T>(name).Global();
}
