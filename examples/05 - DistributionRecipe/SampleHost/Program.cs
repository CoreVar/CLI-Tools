using System.Diagnostics;
using System.Text.Json;
using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Distribution;
using CoreVar.CommandLineInterface.Modules;

var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "sample-release.json")))!;
var root = Environment.GetEnvironmentVariable("COREVAR_CLI_HOME") ?? throw new InvalidOperationException("Run the generated installer first.");
var paths = new DistributionPaths(root);
await CliApp.RunAsync(cli => cli.Version(settings.Version)
    .ModuleManagement(root, Version.Parse(settings.Version))
    .ExternalModules(root)
    .Command("setup", command => command.OnExecute(new Func<CommandExecutionContext, ValueTask<int>>(async context =>
    {
        try
        {
            var manifest = Environment.GetEnvironmentVariable("COREVAR_MODULE_BUNDLE") ?? Path.Combine(AppContext.BaseDirectory, "module-bundle.json");
            var digest = Environment.GetEnvironmentVariable("COREVAR_MODULE_BUNDLE_SHA256") ?? settings.BundleSha256;
            var result = await new ModuleBundleInstaller().InstallAsync(manifest, digest,
                new ModuleInstallContext { HostVersion = Version.Parse(settings.Version), Root = root }, context.CancellationToken);
            if (!result.IsComplete) throw new InvalidOperationException("Required sample module setup failed.");
            await context.Console.WriteLine("Sample setup complete.");
            return 0;
        }
        catch (Exception exception) { await context.Console.WriteErrorLine(exception.Message); return 1; }
    })))
    .Command("update", command => command.OnExecute(new Func<CommandExecutionContext, ValueTask<int>>(async context =>
    {
        try
        {
            var current = JsonSerializer.Deserialize(File.ReadAllText(paths.State), DistributionJsonContext.Default.InstallationState)!;
            var client = new ReleaseCatalogClient();
            var catalog = await client.LoadAsync(current.Catalog, context.CancellationToken);
            var release = ReleaseCatalogClient.Resolve(catalog, current.Channel);
            var coordinator = new ReleaseSetupCoordinator(new DirectUpdater(paths, client, new ArtifactVerifier(JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "publisher-keys.json"))), requireSignature: true)));
            await coordinator.UpdateAndVerifyAsync(current, async (updated, token) =>
            {
                var executable = Path.Combine(paths.Version(updated.Version), OperatingSystem.IsWindows() ? "sample-cli.exe" : "sample-cli");
                var start = new ProcessStartInfo(executable) { UseShellExecute = false };
                start.ArgumentList.Add("setup");
                start.Environment["COREVAR_CLI_HOME"] = root;
                start.Environment["COREVAR_MODULE_BUNDLE"] = release.Bundle!.Manifest.AbsoluteUri;
                start.Environment["COREVAR_MODULE_BUNDLE_SHA256"] = release.Bundle.Sha256;
                using var child = Process.Start(start)!;
                try { await child.WaitForExitAsync(token); }
                catch { if (!child.HasExited) child.Kill(entireProcessTree: true); throw; }
                return child.ExitCode == 0;
            }, release.Version, context.CancellationToken);
            await context.Console.WriteLine($"Sample updated to {release.Version}.");
            return 0;
        }
        catch (Exception exception) { await context.Console.WriteErrorLine(exception.Message); return 1; }
    }))));

internal sealed record Settings(string Version, string BundleSha256);
