using System.Runtime.InteropServices;

namespace CoreVar.CommandLineInterface.Modules;

public static class ModuleCompatibility
{
    public static string CurrentRuntimeIdentifier()
    {
        var platform = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        var architecture = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => "x64", Architecture.X86 => "x86", Architecture.Arm64 => "arm64", Architecture.Arm => "arm",
            var value => value.ToString().ToLowerInvariant()
        };
        return $"{platform}-{architecture}";
    }

    public static bool IsCompatible(ModuleBundleMember member, string runtimeIdentifier, out string? reason)
    {
        var parts = runtimeIdentifier.Split('-', 2);
        var platform = parts[0]; var architecture = parts.Length > 1 ? parts[1] : string.Empty;
        if (member.RuntimeIdentifiers?.Count > 0 && !member.RuntimeIdentifiers.Contains(runtimeIdentifier, StringComparer.OrdinalIgnoreCase))
        { reason = $"Runtime '{runtimeIdentifier}' is not supported."; return false; }
        if (member.Platforms?.Count > 0 && !member.Platforms.Contains(platform, StringComparer.OrdinalIgnoreCase))
        { reason = $"Platform '{platform}' is not supported."; return false; }
        if (member.Architectures?.Count > 0 && !member.Architectures.Contains(architecture, StringComparer.OrdinalIgnoreCase))
        { reason = $"Architecture '{architecture}' is not supported."; return false; }
        reason = null; return true;
    }
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
