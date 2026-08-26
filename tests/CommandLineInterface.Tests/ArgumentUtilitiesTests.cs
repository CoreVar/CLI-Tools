using CoreVar.CommandLineInterface.Utilities;

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
}
