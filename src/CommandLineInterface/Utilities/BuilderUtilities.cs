using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Utilities;

public class BuilderUtilities
{

    public static bool TryGetStringOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(String)} value must be a length of 1.");
        value = context.Arguments[option.Position + 1];
        return true;
    }

    public static bool TryGetBooleanOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength < 0 || option.ValueLength > 1)
            throw new InvalidOperationException($"The {nameof(Boolean)} value must be a length of 0 or 1.");
        if (option.ValueLength == 1)
        {
            var result = bool.TryParse(context.Arguments[option.Position + 1], out var parseResult);
            value = parseResult;
            return result;
        }
        value = true;
        return true;
    }

    public static bool TryGetByteOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(Byte)} value must be a length of 1.");
        var result = byte.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetUInt16Option(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(UInt16)} value must be a length of 1.");
        var result = ushort.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetUInt32Option(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(UInt32)} value must be a length of 1.");
        var result = uint.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetUInt64Option(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(UInt64)} value must be a length of 1.");
        var result = ulong.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetSByteOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(SByte)} value must be a length of 1.");
        var result = sbyte.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetInt16Option(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(Int16)} value must be a length of 1.");
        var result = short.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetInt32Option(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(Int32)} value must be a length of 1.");
        var result = int.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetInt64Option(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(Int64)} value must be a length of 1.");
        var result = long.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetSingleOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(Single)} value must be a length of 1.");
        var result = float.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetDoubleOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(Double)} value must be a length of 1.");
        var result = double.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetDecimalOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(Decimal)} value must be a length of 1.");
        var result = decimal.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetGuidOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(Guid)} value must be a length of 1.");
        var result = Guid.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetDateTimeOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(DateTime)} value must be a length of 1.");
        var result = DateTime.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetDateTimeOffsetOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(DateTimeOffset)} value must be a length of 1.");
        var result = DateTimeOffset.TryParse(context.Arguments[option.Position + 1], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetBase64ByteArrayOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(DateTimeOffset)} value must be a length of 1.");
        var base64String = context.Arguments[option.Position + 1];
        byte[] buffer = new byte[((base64String.Length * 3) + 3) / 4 - (base64String.Length > 0 && base64String[base64String.Length - 1] == '=' ? base64String.Length > 1 && base64String[base64String.Length - 2] == '=' ? 2 : 1 : 0)];
        var result = Convert.TryFromBase64String(base64String, buffer, out _);
        value = buffer;
        return result;
    }

    public static bool TryGetHexByteArrayOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        if (option.ValueLength != 1)
            throw new InvalidOperationException($"The {nameof(DateTimeOffset)} value must be a length of 1.");
        try
        {
            value = Convert.FromHexString(context.Arguments[option.Position + 1]);
            return true;
        }
        catch
        {
            value = default!;
            return false;
        }
    }

    public static bool TryGetByteArrayOption(CommandExecutionContext context, CommandTreeOptionContext option, out object value)
    {
        throw new InvalidOperationException($"A byte array format must be specified, like {nameof(ICommandOptionBuilder)}.{CommandOptionBuilderExtensions.WithBase64Encoding} or {nameof(ICommandOptionBuilder)}.{nameof(CommandOptionBuilderExtensions.WithHexEncoding)}.");
    }

    public static bool TryGetStringArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(String)} value must be a length of 1.");
        value = context.Arguments[argument.ValueRange.Start.Value];
        return true;
    }

    public static bool TryGetBooleanArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(Boolean)} value must be a length of 0 or 1.");
        
        var result = bool.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetByteArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(Byte)} value must be a length of 1.");
        var result = byte.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetUInt16Argument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(UInt16)} value must be a length of 1.");
        var result = ushort.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetUInt32Argument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(UInt32)} value must be a length of 1.");
        var result = uint.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetUInt64Argument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(UInt64)} value must be a length of 1.");
        var result = ulong.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetSByteArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(SByte)} value must be a length of 1.");
        var result = sbyte.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetInt16Argument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(Int16)} value must be a length of 1.");
        var result = short.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetInt32Argument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(Int32)} value must be a length of 1.");
        var result = int.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetInt64Argument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(Int64)} value must be a length of 1.");
        var result = long.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetSingleArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(Single)} value must be a length of 1.");
        var result = float.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetDoubleArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(Double)} value must be a length of 1.");
        var result = double.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetDecimalArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(Decimal)} value must be a length of 1.");
        var result = decimal.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetGuidArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(Guid)} value must be a length of 1.");
        var result = Guid.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetDateTimeArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(DateTime)} value must be a length of 1.");
        var result = DateTime.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetDateTimeOffsetArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(DateTimeOffset)} value must be a length of 1.");
        var result = DateTimeOffset.TryParse(context.Arguments[argument.ValueRange.Start.Value], out var parseResult);
        value = parseResult;
        return result;
    }

    public static bool TryGetBase64ByteArrayArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(DateTimeOffset)} value must be a length of 1.");
        var base64String = context.Arguments[argument.ValueRange.Start.Value];
        byte[] buffer = new byte[((base64String.Length * 3) + 3) / 4 - (base64String.Length > 0 && base64String[base64String.Length - 1] == '=' ? base64String.Length > 1 && base64String[base64String.Length - 2] == '=' ? 2 : 1 : 0)];
        var result = Convert.TryFromBase64String(base64String, buffer, out _);
        value = buffer;
        return result;
    }

    public static bool TryGetHexByteArrayArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        var valueLength = argument.ValueRange.End.Value - argument.ValueRange.Start.Value;
        if (valueLength != 1)
            throw new InvalidOperationException($"The {nameof(DateTimeOffset)} value must be a length of 1.");
        try
        {
            value = Convert.FromHexString(context.Arguments[argument.ValueRange.Start.Value]);
            return true;
        }
        catch
        {
            value = default!;
            return false;
        }
    }

    public static bool TryGetByteArrayArgument(CommandExecutionContext context, CommandTreeArgumentContext argument, out object value)
    {
        throw new InvalidOperationException($"A byte array format must be specified, like {nameof(ICommandOptionBuilder)}.{CommandOptionBuilderExtensions.WithBase64Encoding} or {nameof(ICommandOptionBuilder)}.{nameof(CommandOptionBuilderExtensions.WithHexEncoding)}.");
    }
}
