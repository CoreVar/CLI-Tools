namespace CoreVar.CommandLineInterface.Distribution;

public sealed class DistributionPaths(string root)
{
    public string Root { get; } = Path.GetFullPath(root);
    public string Versions => Path.Combine(Root, "versions");
    public string Cache => Path.Combine(Root, "cache");
    public string State => Path.Combine(Root, "state.json");
    public string Version(string version) => Path.Combine(Versions, SafeSegment(version));

    internal static string SafeSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || value.Contains('/') || value.Contains('\\') || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("The value must be a safe path segment.", nameof(value));
        return value;
    }
}
