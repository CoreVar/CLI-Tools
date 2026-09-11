using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Testing;

namespace CommandLineInterface.Tests;

public sealed class CommandMetadataTests
{
    [Fact]
    public async Task Host_backed_root_help_contains_all_fluent_commands()
    {
        var result = await Builder().TestAsync("--help");
        Assert.Contains("account", result.Output);
        Assert.Contains("module", result.Output);
    }

    [Fact]
    public async Task Host_backed_powershell_completion_contains_all_fluent_commands()
    {
        var result = await Builder().TestAsync("completion powershell");
        Assert.Contains("'account'", result.Output);
        Assert.Contains("'show'", result.Output);
        Assert.Contains("'module'", result.Output);
        Assert.Contains("'list'", result.Output);
    }

    private static CommandLineBuilder Builder()
    {
        var builder = CommandLineBuilder.Create();
        builder.Command("account", account => account.Description("Account commands")
            .Command("show", command => command.Description("Show account").OnExecute(_ => ValueTask.CompletedTask)));
        builder.Command("module", module => module.Description("Module commands")
            .Command("list", command => command.Description("List modules").OnExecute(_ => ValueTask.CompletedTask)));
        return builder;
    }
}
