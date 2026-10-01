using System.Diagnostics;
using System.Text.Json;
using CoreVar.CommandLineInterface.Distribution;

var configuredRoot = Environment.GetEnvironmentVariable("COREVAR_CLI_HOME");
var root = string.IsNullOrWhiteSpace(configuredRoot)
    ? Directory.GetParent(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName ?? AppContext.BaseDirectory
    : Path.GetFullPath(configuredRoot);
var paths = new DistributionPaths(root);
if (!File.Exists(paths.State))
{
    Console.Error.WriteLine($"CoreVar CLI installation state was not found at '{paths.State}'.");
    return 78;
}

InstallationState? state;
await using (var stream = File.OpenRead(paths.State))
    state = await JsonSerializer.DeserializeAsync(stream, DistributionJsonContext.Default.InstallationState);
if (state is null)
{
    Console.Error.WriteLine("CoreVar CLI installation state is invalid.");
    return 78;
}

var executableName = state.Entrypoint ?? state.Product;
if (OperatingSystem.IsWindows() && string.IsNullOrEmpty(Path.GetExtension(executableName))) executableName += ".exe";
var executable = Path.GetFullPath(Path.Combine(paths.Version(state.Version), executableName));
var versionRoot = Path.GetFullPath(paths.Version(state.Version)) + Path.DirectorySeparatorChar;
if (!executable.StartsWith(versionRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(executable))
{
    Console.Error.WriteLine($"The active CLI executable for version {state.Version} is missing.");
    return 78;
}

var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Environment.CurrentDirectory };
foreach (var argument in args) start.ArgumentList.Add(argument);
start.Environment["COREVAR_CLI_HOME"] = root;
start.Environment["COREVAR_CLI_ACTIVE_VERSION"] = state.Version;
start.Environment["COREVAR_CLI_LAUNCHER_VERSION"] = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0";
using var child = Process.Start(start);
if (child is null) return 70;
await child.WaitForExitAsync();
return child.ExitCode;
