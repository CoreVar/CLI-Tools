using CoreVar.CommandLineInterface.Utilities;
using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Support;

namespace CommandLineInterface.Tests;

public class ArgumentUtilitiesTests
{
    [Fact]
    public void ParseArgumentsPreservesWindowsPaths()
    {
        var arguments = ArgumentUtilities.ParseArguments(@"deploy C:\temp\appsettings.json");

        Assert.Equal(["deploy", @"C:\temp\appsettings.json"], arguments);
    }

    [Fact]
    public void ParseArgumentsPreservesEmptyAndQuotedArguments()
    {
        var arguments = ArgumentUtilities.ParseArguments("run \"\" \"two words\"");

        Assert.Equal(["run", "", "two words"], arguments);
    }

    [Fact]
    public void ParseArgumentsRejectsUnterminatedQuotes()
        => Assert.Throws<FormatException>(() => ArgumentUtilities.ParseArguments("run \"unfinished"));

    [Theory]
    [InlineData("")]
    [InlineData("plain")]
    [InlineData("two words")]
    [InlineData("quoted\"value")]
    [InlineData(@"C:\Program Files\CoreVar")]
    public void ArgumentsCanRoundTrip(string value)
    {
        var commandLine = ArgumentUtilities.ConvertToArgumentsString([value]);

        Assert.Equal([value], ArgumentUtilities.ParseArguments(commandLine));
    }

    [Fact]
    public void EndOfOptionsMakesOptionLikeTokenPositional()
    {
        var builder = CommandLineBuilder.Create("test");
        builder.Argument<string>("value").IsRequired();
        builder.OnExecute(_ => { });

        var context = CommandTreeBuilder.BuildAndLoad(builder, ["--", "--literal"]);

        Assert.Equal("--literal", context.Root.Arguments!["value"].Values.Single());
        Assert.Null(context.Root.Options);
        Assert.Empty(context.Validate());
    }

    [Fact]
    public void ExpandsLongEqualsAndCompactFlags()
        => Assert.Equal(["--output", "json", "-a", "-b"], ArgumentUtilities.ExpandArguments(["--output=json", "-ab"]));

    [Fact]
    public void OptionsCanFollowVariadicValues()
    {
        var builder = CommandLineBuilder.Create("test");
        builder.Argument<string[]>("values").Variadic();
        builder.Option<int>("--count").Default(1);
        builder.OnExecute(_ => { });

        var context = CommandTreeBuilder.BuildAndLoad(builder, ["Ada", "Grace", "--count", "2"]);

        Assert.Equal(["Ada", "Grace"], context.Root.Arguments!["values"].Values);
        Assert.Equal(["2"], context.Root.Options!["--count"].Values);
        Assert.Empty(context.Validate());
    }
}
