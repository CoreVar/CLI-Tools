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
        Assert.True(command.Options!["output"].IsRequired);
        Assert.Contains("o", command.Options!["output"].Aliases!);
        Assert.Equal("output", command.OptionAliases!["o"]);
    }
}

[CommandName("generated")]
public class GeneratedMetadataComponent : CommandLineComponent
{
    [CommandArgument("second", Index = 1), Optional]
    public string Second { get; set; } = "fallback";

    [CommandArgument("first", Index = 0), Required]
    public string? First { get; set; }

    [CommandOption("output"), CommandOptionAlias("o"), Required]
    public string? Output { get; set; }

    public Task ExecuteAsync() => Task.CompletedTask;
}

[Component<GeneratedMetadataComponent>]
public partial class GeneratedMetadataContext : ComponentContext;
