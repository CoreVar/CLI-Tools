using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Utilities;

namespace CoreVar.CommandLineInterface;

public static class CommandOptionBuilderExtensions
{

    /// <summary>
    /// Defines the command option as having Base64 encoding.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder.</returns> 
    public static ICommandOptionBuilder<byte[]> WithBase64Encoding(this ICommandOptionBuilder<byte[]> builder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.GetValueHandler = BuilderUtilities.TryGetBase64ByteArrayOption;
        return builder;
    }

    /// <summary>
    /// Defines the command option as having hexadecimal encoding.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder.</returns>
    public static ICommandOptionBuilder<byte[]> WithHexEncoding(this ICommandOptionBuilder<byte[]> builder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.GetValueHandler = BuilderUtilities.TryGetHexByteArrayOption;
        return builder;
    }

    /// <summary>
    /// Defines the command option as being required.
    /// </summary>
    /// <typeparam name="T">The type of the option value.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder.</returns>
    public static ICommandOptionBuilder<T> IsRequired<T>(this ICommandOptionBuilder<T> builder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.IsRequired = true;
        return builder;
    }

    /// <summary>
    /// Defines the command option as being optional.
    /// </summary>
    /// <typeparam name="T">The type of the option value.</typeparam>
    /// <param name="builder"></param>
    /// <returns></returns>
    public static ICommandOptionBuilder<T> IsOptional<T>(this ICommandOptionBuilder<T> builder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.IsRequired = false;
        return builder;
    }

    /// <summary>
    /// Defines alias(es) for the command option.
    /// </summary>
    /// <typeparam name="T">The type of the option value.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="aliases">The aliases for the option.</param>
    /// <returns>The builder.</returns>
    public static ICommandOptionBuilder<T> WithAlias<T>(this ICommandOptionBuilder<T> builder, params string[] aliases)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.Aliases ??= new(builderInternals.CommandLineOptions.OptionComparer);
        foreach (var alias in aliases)
            builderInternals.Aliases.Add(alias);
        return builder;
    }

}
