using System.Globalization;

namespace CoreVar.CommandLineInterface.Utilities;

internal static class ValueConverter
{
    public static bool TryConvert(string? text, Type targetType, out object? value)
    {
        var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (text is null)
        {
            value = null;
            return !effectiveType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null;
        }

        if (effectiveType == typeof(string))
        {
            value = text;
            return true;
        }

        if (effectiveType == typeof(FileInfo))
        {
            value = new FileInfo(text);
            return true;
        }

        if (effectiveType == typeof(DirectoryInfo))
        {
            value = new DirectoryInfo(text);
            return true;
        }

        if (effectiveType == typeof(Uri))
        {
            var success = Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri);
            value = uri;
            return success;
        }

        if (effectiveType.IsEnum)
        {
            var success = Enum.TryParse(effectiveType, text, ignoreCase: true, out var parsed);
            value = parsed;
            return success;
        }

        if (effectiveType == typeof(Guid) && Guid.TryParse(text, out var guid)) { value = guid; return true; }
        if (effectiveType == typeof(DateTime) && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTime)) { value = dateTime; return true; }
        if (effectiveType == typeof(DateTimeOffset) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dateTimeOffset)) { value = dateTimeOffset; return true; }
        if (effectiveType == typeof(DateOnly) && DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly)) { value = dateOnly; return true; }
        if (effectiveType == typeof(TimeOnly) && TimeOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeOnly)) { value = timeOnly; return true; }
        if (effectiveType == typeof(TimeSpan) && TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var timeSpan)) { value = timeSpan; return true; }
        if (effectiveType == typeof(bool) && bool.TryParse(text, out var boolean)) { value = boolean; return true; }
        if (effectiveType == typeof(byte) && byte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var byteValue)) { value = byteValue; return true; }
        if (effectiveType == typeof(sbyte) && sbyte.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sbyteValue)) { value = sbyteValue; return true; }
        if (effectiveType == typeof(short) && short.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var shortValue)) { value = shortValue; return true; }
        if (effectiveType == typeof(ushort) && ushort.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ushortValue)) { value = ushortValue; return true; }
        if (effectiveType == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue)) { value = intValue; return true; }
        if (effectiveType == typeof(uint) && uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uintValue)) { value = uintValue; return true; }
        if (effectiveType == typeof(long) && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue)) { value = longValue; return true; }
        if (effectiveType == typeof(ulong) && ulong.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ulongValue)) { value = ulongValue; return true; }
        if (effectiveType == typeof(float) && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue)) { value = floatValue; return true; }
        if (effectiveType == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue)) { value = doubleValue; return true; }
        if (effectiveType == typeof(decimal) && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue)) { value = decimalValue; return true; }

        value = null;
        return false;
    }
}
