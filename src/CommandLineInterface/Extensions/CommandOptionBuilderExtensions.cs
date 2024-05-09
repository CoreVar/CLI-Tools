using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Utilities;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

public static class CommandOptionBuilderExtensions
{

    public static ICommandOptionBuilder<byte[]> WithBase64Encoding(this ICommandOptionBuilder<byte[]> builder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.GetValueHandler = BuilderUtilities.TryGetBase64ByteArrayOption;
        return builder;
    }

    public static ICommandOptionBuilder<byte[]> WithHexEncoding(this ICommandOptionBuilder<byte[]> builder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.GetValueHandler = BuilderUtilities.TryGetHexByteArrayOption;
        return builder;
    }

    public static ICommandOptionBuilder<T> IsRequired<T>(this ICommandOptionBuilder<T> builder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.IsRequired = true;
        return builder;
    }

    public static ICommandOptionBuilder<T> IsOptional<T>(this ICommandOptionBuilder<T> builder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.IsRequired = false;
        return builder;
    }

    public static ICommandOptionBuilder<T> WithAlias<T>(this ICommandOptionBuilder<T> builder, params string[] aliases)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.Aliases ??= new(builderInternals.CommandLineOptions.OptionComparer);
        foreach (var alias in aliases)
            builderInternals.Aliases.Add(alias);
        return builder;
    }

}
