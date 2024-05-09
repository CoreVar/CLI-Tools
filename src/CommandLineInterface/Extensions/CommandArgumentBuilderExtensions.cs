using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

public static class CommandArgumentBuilderExtensions
{

    public static ICommandArgumentBuilder<byte[]> WithBase64Encoding(this ICommandArgumentBuilder<byte[]> builder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.GetValueHandler = BuilderUtilities.TryGetBase64ByteArrayOption;
        return builder;
    }

    public static ICommandArgumentBuilder<byte[]> WithHexEncoding(this ICommandArgumentBuilder<byte[]> builder)
    {
        var builderInternals = (ICommandOptionBuilderInternals)builder;
        builderInternals.GetValueHandler = BuilderUtilities.TryGetHexByteArrayOption;
        return builder;
    }

}
