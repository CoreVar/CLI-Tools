using Microsoft.Extensions.DependencyInjection;

namespace CoreVar.CommandLineInterface.Testing;

/// <summary>Provides concise, host-backed command testing.</summary>
public static class CliTestingExtensions
{
    public static async ValueTask<CliTestResult> TestAsync(this CommandLineBuilder builder, string commandLine, IEnumerable<string>? input = null)
    {
        var console = new TestConsoleControl(input);
        builder.SetupHostBuilder(host => host.Services.AddSingleton<IConsoleControl>(console));
        await using var app = builder.Build(() => Utilities.ArgumentUtilities.ParseArguments(commandLine));
        await app.RunAsync();
        return new CliTestResult(Environment.ExitCode, console.Output, console.Error);
    }
}
