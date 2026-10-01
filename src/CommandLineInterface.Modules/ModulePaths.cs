namespace CoreVar.CommandLineInterface.Modules;

public sealed class ModulePaths(string? root = null)
{
    public string Root { get; } = Path.GetFullPath(root ?? DefaultRoot());
    public string Modules => Path.Combine(Root, "modules");
    public string Cache => Path.Combine(Root, "cache");
    public string Module(string id) => Path.Combine(Modules, ValidateSegment(id));
    public string Versions(string id) => Path.Combine(Module(id), "versions");
    public string Version(string id, string version) => Path.Combine(Versions(id), ValidateSegment(version));
    public string Current(string id) => Path.Combine(Module(id), "current.json");

    private static string DefaultRoot()
    {
        var configured = Environment.GetEnvironmentVariable("COREVAR_CLI_HOME");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath)) basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(basePath, "CoreVar", "Cli");
    }

    internal static string ValidateSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.Contains('/') || value.Contains('\\'))
            throw new ArgumentException("The value must be a safe path segment.", nameof(value));
        return value;
    }
}
