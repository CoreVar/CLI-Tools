using CoreVar.CommandLineInterface.Builders;

namespace CoreVar.CommandLineInterface.Modules;

public sealed class ModuleBundleBootstrapOptions
{
    public required string Manifest { get; set; }
    public required string Sha256 { get; set; }
    public required string HostVersion { get; set; }
    public required string Snapshot { get; set; }
    public string? FrameworkVersion { get; set; }
    public string? Root { get; set; }
    public bool Force { get; set; }
}

public static class ModuleBundleExtensions
{
    /// <summary>Installs a pinned local or HTTPS module bundle before the command tree is projected.</summary>
    public static ICommandLineBuilder UseModuleBundleBootstrap(this ICommandLineBuilder builder,
        ModuleBundleBootstrapOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var root = options.Root ?? new ModulePaths().Root;
        var statePath = Path.Combine(root, "bundle-state.json");
        if (!options.Force && File.Exists(statePath))
        {
            var state = System.Text.Json.JsonSerializer.Deserialize(File.ReadAllText(statePath), ModuleJsonContext.Default.ModuleBundleState);
            if (state?.Snapshot == options.Snapshot && ModuleBundleClient.Normalize(state.Sha256)
                    .Equals(ModuleBundleClient.Normalize(options.Sha256), StringComparison.OrdinalIgnoreCase))
                return builder;
        }
        var context = new ModuleInstallContext
        {
            HostVersion = Version.Parse(options.HostVersion),
            FrameworkVersion = options.FrameworkVersion is null
                ? typeof(ModuleBundleExtensions).Assembly.GetName().Version ?? new(1, 0)
                : Version.Parse(options.FrameworkVersion),
            Root = root
        };
        var result = new ModuleBundleInstaller().InstallAsync(options.Manifest, options.Sha256, context)
            .AsTask().GetAwaiter().GetResult();
        if (!result.IsComplete)
        {
            var failures = string.Join("; ", result.Items.Where(item => item.Required && item.Status != ModuleBundleItemStatus.Installed)
                .Select(item => $"{item.Id}: {item.Message ?? item.Status.ToString()}"));
            throw new InvalidOperationException($"Required module bundle installation failed: {failures}");
        }
        return builder;
    }

    public static ICommandLineBuilder UseModuleBundleBootstrap(this ICommandLineBuilder builder,
        Action<ModuleBundleBootstrapOptionsBuilder> configure)
    {
        var options = new ModuleBundleBootstrapOptionsBuilder();
        configure(options);
        return builder.UseModuleBundleBootstrap(options.Build());
    }
}

public sealed class ModuleBundleBootstrapOptionsBuilder
{
    private string? _manifest, _sha256, _hostVersion, _snapshot;
    private string? _frameworkVersion, _root;
    private bool _force;
    public ModuleBundleBootstrapOptionsBuilder Manifest(string pathOrHttpsUri, string sha256) { _manifest = pathOrHttpsUri; _sha256 = sha256; return this; }
    public ModuleBundleBootstrapOptionsBuilder HostVersion(string version) { _hostVersion = version; return this; }
    public ModuleBundleBootstrapOptionsBuilder Snapshot(string snapshot) { _snapshot = snapshot; return this; }
    public ModuleBundleBootstrapOptionsBuilder FrameworkVersion(string version) { _frameworkVersion = version; return this; }
    public ModuleBundleBootstrapOptionsBuilder Root(string path) { _root = path; return this; }
    public ModuleBundleBootstrapOptionsBuilder Force(bool force = true) { _force = force; return this; }
    internal ModuleBundleBootstrapOptions Build() => new()
    {
        Manifest = _manifest ?? throw new InvalidOperationException("A bundle manifest is required."),
        Sha256 = _sha256 ?? throw new InvalidOperationException("A bundle SHA-256 digest is required."),
        HostVersion = _hostVersion ?? throw new InvalidOperationException("An explicit host version is required."),
        Snapshot = _snapshot ?? throw new InvalidOperationException("An explicit bundle snapshot is required."),
        FrameworkVersion = _frameworkVersion, Root = _root, Force = _force
    };
}
