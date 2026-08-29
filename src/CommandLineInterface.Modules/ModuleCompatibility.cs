namespace CoreVar.CommandLineInterface.Modules;

public static class ModuleCompatibility
{
    /// <summary>Evaluates a NuGet-style inclusive/exclusive version interval such as [11.0.0,12.0.0).</summary>
    public static bool IsCompatible(string? range, Version hostVersion)
    {
        if (string.IsNullOrWhiteSpace(range) || range == "*") return true;
        range = range.Trim();
        if (Version.TryParse(range, out var exact)) return hostVersion == exact;
        if (range.Length < 3 || range[0] is not ('[' or '(') || range[^1] is not (']' or ')'))
            throw new FormatException($"Invalid CLI compatibility range '{range}'.");
        var limits = range[1..^1].Split(',', 2);
        if (limits.Length != 2) throw new FormatException($"Invalid CLI compatibility range '{range}'.");
        if (Version.TryParse(limits[0].Trim(), out var minimum))
        {
            var comparison = hostVersion.CompareTo(minimum);
            if (comparison < 0 || comparison == 0 && range[0] == '(') return false;
        }
        if (Version.TryParse(limits[1].Trim(), out var maximum))
        {
            var comparison = hostVersion.CompareTo(maximum);
            if (comparison > 0 || comparison == 0 && range[^1] == ')') return false;
        }
        return true;
    }
}
