using System.Diagnostics;
using System.Text.Json;
using CoreVar.CommandLineInterface.Builders;

namespace CoreVar.CommandLineInterface.Distribution;

public sealed class SelfUpdateOptions
{
    public required string Product { get; init; }
    public required string CurrentVersion { get; init; }
    public required Uri Catalog { get; init; }
    public string Channel { get; init; } = "stable";
    public string? Root { get; init; }
    public InstallationProvider Provider { get; init; } = InstallationProvider.Direct;
    public string? PackageId { get; init; }
    public string? Entrypoint { get; init; }
    /// <summary>Publisher public keys, keyed by the stable signing key ID. Never include private keys.</summary>
    public IReadOnlyDictionary<string, string> TrustedPublicKeys { get; init; } = new Dictionary<string, string>();
    /// <summary>Reject unsigned artifacts. Defaults to false for local and unsigned community builds.</summary>
    public bool RequireSignature { get; init; }
    /// <summary>Runs the candidate host's setup/health check before activation. Required for bundled releases.</summary>
    public Func<InstallationState, CancellationToken, ValueTask<bool>>? Readiness { get; init; }
}

public static class SelfUpdateExtensions
{
    /// <summary>Adds check, install, and rollback commands for CLI self-update.</summary>
    public static ICommandLineBuilder SelfUpdate(this ICommandLineBuilder builder, SelfUpdateOptions options)
    {
        return builder.Command("update", update =>
        {
            update.Description("Checks for and installs CLI updates.");
            update.Command("check", command => command.OnExecute(new Func<CommandExecutionContext, ValueTask>(async context =>
            {
                var state = await LoadStateAsync(options, context.CancellationToken);
                var catalog = await new ReleaseCatalogClient().LoadAsync(state.Catalog, context.CancellationToken);
                var release = ReleaseCatalogClient.Resolve(catalog, state.Channel, installationId: state.InstallationId);
                await context.Console.WriteLine(release.Version.Equals(state.Version, StringComparison.OrdinalIgnoreCase)
                    ? $"{state.Product} {state.Version} is up to date."
                    : $"{state.Product} {release.Version} is available (installed: {state.Version}).");
            })));
            update.Command("rollback", command => command.OnExecute(new Func<CommandExecutionContext, ValueTask>(async context =>
            {
                var state = await LoadStateAsync(options, context.CancellationToken);
                var updater = CreateUpdater(options);
                var rolledBack = await updater.RollbackAsync(state, context.CancellationToken);
                await context.Console.WriteLine($"Activated {rolledBack.Version}. Restart the CLI to use it.");
            })));
            update.OnExecute(new Func<CommandExecutionContext, ValueTask>(async context =>
            {
                var state = await LoadStateAsync(options, context.CancellationToken);
                var native = NativeUpdateCommands.Create(state);
                if (native is not null)
                {
                    await context.Console.WriteLine(native.Explanation);
                    context.Result = await RunAsync(native, context.CancellationToken);
                    return;
                }
                var updated = await CreateUpdater(options).UpdateAsync(state, cancellationToken: context.CancellationToken, readiness: options.Readiness);
                await context.Console.WriteLine(updated.Version == state.Version ? "Already up to date." : $"Installed {updated.Version}. Restart the CLI to use it.");
            }));
        });
    }

    private static DirectUpdater CreateUpdater(SelfUpdateOptions options) => new(new DistributionPaths(Root(options)), new ReleaseCatalogClient(), new ArtifactVerifier(options.TrustedPublicKeys, options.RequireSignature));

    private static async ValueTask<InstallationState> LoadStateAsync(SelfUpdateOptions options, CancellationToken cancellationToken)
    {
        var paths = new DistributionPaths(Root(options));
        if (File.Exists(paths.State))
        {
            await using var stream = File.OpenRead(paths.State);
            if (await JsonSerializer.DeserializeAsync(stream, DistributionJsonContext.Default.InstallationState, cancellationToken) is { } existing) return existing;
        }
        return new InstallationState
        {
            Product = options.Product, Version = options.CurrentVersion, Catalog = options.Catalog,
            Channel = options.Channel, Provider = options.Provider, PackageId = options.PackageId, Entrypoint = options.Entrypoint,
            LauncherVersion = Environment.GetEnvironmentVariable("COREVAR_CLI_LAUNCHER_VERSION")
        };
    }

    private static string Root(SelfUpdateOptions options) => options.Root
        ?? Environment.GetEnvironmentVariable("COREVAR_CLI_HOME")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), options.Product);

    private static async ValueTask<int> RunAsync(NativeUpdateCommand command, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(command.Executable) { UseShellExecute = false };
        foreach (var argument in command.Arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {command.Executable}.");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }
}
