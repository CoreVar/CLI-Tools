using CoreVar.CommandLineInterface;

namespace CommandLineInterface.Tests;

public class BlazorConsoleControlTests
{
    [Fact]
    public async Task SubmitLineEchoesAndCompletesReader()
    {
        var console = new BlazorConsoleControl();
        var read = console.ReadLine().AsTask();

        console.SubmitLine("status");

        Assert.Equal("status", await read);
        Assert.Equal("status", console.Lines[0].Elements.Single().Text);
        Assert.Empty(console.Lines[^1].Elements);
    }

    [Fact]
    public async Task CancelLineCancelsInterruptAndCompletesReader()
    {
        var console = new BlazorConsoleControl();
        var read = console.ReadLine().AsTask();

        console.CancelLine();

        Assert.True(console.InterruptToken.IsCancellationRequested);
        Assert.Equal(string.Empty, await read);
        Assert.Equal("^C", console.Lines[0].Elements.Last().Text);
        console.ResetInterrupt();
        Assert.False(console.InterruptToken.IsCancellationRequested);
    }

    [Fact]
    public async Task UnderstandsAnsiColorCarriageReturnAndBackspace()
    {
        var console = new BlazorConsoleControl();

        await console.Write("old\r\u001b[31;1mnew!\b\u001b[0m");

        var element = Assert.Single(console.Lines[0].Elements);
        Assert.Equal("new", element.Text);
        Assert.Equal(System.Drawing.Color.Maroon, element.Color);
        Assert.True(element.IsBold);
    }

    [Fact]
    public async Task TrimsScrollbackButKeepsCurrentLine()
    {
        var console = new BlazorConsoleControl { MaxLines = 3 };
        for (var index = 0; index < 8; index++) await console.WriteLine(index.ToString());

        Assert.Equal(3, console.Lines.Count);
        Assert.Equal("6", console.Lines[0].Elements.Single().Text);
        Assert.Equal("7", console.Lines[1].Elements.Single().Text);
        Assert.Empty(console.Lines[2].Elements);
    }
}
