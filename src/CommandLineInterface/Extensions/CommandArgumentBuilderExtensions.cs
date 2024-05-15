using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Utilities;

namespace CoreVar.CommandLineInterface;

public static class CommandArgumentBuilderExtensions
{

    /// <summary>
    /// Defines the command argument as having Base64 encoding.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder.</returns>
    public static ICommandArgumentBuilder<byte[]> WithBase64Encoding(this ICommandArgumentBuilder<byte[]> builder)
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
    public static ICommandArgumentBuilder<byte[]> WithHexEncoding(this ICommandArgumentBuilder<byte[]> builder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.GetValueHandler = BuilderUtilities.TryGetHexByteArrayOption;
        return builder;
    }

}
