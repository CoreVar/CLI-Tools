using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace CommandLineInterface.Tests;

public class SourceGeneratorTests
{
    [Fact]
    public async Task GeneratedMetadataHonorsAttributes()
    {
        var builder = CommandLineBuilder.Create("test");
        builder.Components<GeneratedMetadataContext>();

        await using var app = builder.Build(() => ["generated"]);
        var command = app.Host.Services.GetRequiredService<CommandTree>().Root.Children["generated"];

        var arguments = Assert.IsType<List<CommandTreeArgument>>(command.Arguments);
        Assert.Equal(["first", "second"], arguments.Select(argument => argument.Name));
        Assert.True(arguments[0].IsRequired);
        Assert.False(arguments[1].IsRequired);
        Assert.NotNull(command.Options);
        Assert.False(command.Options!["output"].IsRequired); // alternate sources make the CLI token optional
        Assert.Contains("o", command.Options!["output"].Aliases!);
        Assert.Equal("output", command.OptionAliases!["o"]);
        Assert.Contains("gen", command.Aliases);
        Assert.True(command.IsHidden);
        Assert.Equal("Use generated-v2.", command.DeprecationMessage);
        Assert.Equal("CLI_OUTPUT", command.Options["output"].EnvironmentVariable);
        Assert.Equal("Generated:Output", command.Options["output"].ConfigurationKey);
        Assert.True(command.Options["output"].IsGlobal);
        Assert.Contains("json", command.Options["output"].Completions);
        Assert.True(arguments[1].IsVariadic);
        Assert.Contains("tail", arguments[1].Completions);
    }
}

[CommandName("generated", Aliases = ["gen"], Hidden = true, Deprecated = "Use generated-v2.")]
public class GeneratedMetadataComponent : CommandLineComponent
{
    [CommandArgument("second", Index = 1, Variadic = true, Completions = ["tail"]), Optional]
    public string Second { get; set; } = "fallback";

    [CommandArgument("first", Index = 0), Required]
    public string? First { get; set; }

    [CommandOption("output", EnvironmentVariable = "CLI_OUTPUT", ConfigurationKey = "Generated:Output", Global = true, Completions = ["json", "text"]), CommandOptionAlias("o"), Required]
    public string? Output { get; set; }

    public Task ExecuteAsync() => Task.CompletedTask;
}

[Component<GeneratedMetadataComponent>]
public partial class GeneratedMetadataContext : ComponentContext;
