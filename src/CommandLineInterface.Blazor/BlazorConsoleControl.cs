using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CoreVar.CommandLineInterface.Services;

namespace CoreVar.CommandLineInterface;

public class BlazorConsoleControl : IConsoleControl
{
    private TaskCompletionSource<string> _readLineTaskCompletionSource = new();
    private static ulong _key = 0;

    public event EventHandler? LinesChanged;

    public static ulong NewKey()
        => Interlocked.Increment(ref _key);

    public List<ConsoleLine> Lines { get; } = [new ConsoleLine
    {
        Elements = {
            new ConsoleTextElement
            {
                Text = string.Empty,
                Color = Color.LightGray
            }
        }
    }];

    public void SubmitLine(string text)
    {
        ParseAndWrite(text);
        NewLine();

        var completionSource = _readLineTaskCompletionSource;

        _readLineTaskCompletionSource = new();
        completionSource.TrySetResult(text);
    }

    public async ValueTask<string> ReadLine()
    {
        return await _readLineTaskCompletionSource.Task;
    }

    private void ParseAndWrite(string text)
    {
        var lines = text.Split('\n');

        var lastLine = Lines.Last();
        lastLine.Elements.Add(new ConsoleTextElement
        {
            Text = lines[0],
            Color = Color.LightGray
        });

        foreach (var line in lines.Skip(1))
        {
            Lines.Add(new ConsoleLine
            {
                Elements =
                {
                    new ConsoleTextElement
                    {
                        Text = line,
                        Color = Color.LightGray
                    }
                }
            });
        }
    }

    private void NewLine()
    {
        Lines.Add(new ConsoleLine());
    }

    public ValueTask Write(string text)
    {
        ParseAndWrite(text);

        LinesChanged?.Invoke(this, EventArgs.Empty);

        return ValueTask.CompletedTask;
    }

    public ValueTask WriteErrorLine(string text)
    {
        ParseAndWrite(text);
        NewLine();

        LinesChanged?.Invoke(this, EventArgs.Empty);

        return ValueTask.CompletedTask;
    }

    public ValueTask WriteLine(string text)
    {
        ParseAndWrite(text);
        NewLine();

        LinesChanged?.Invoke(this, EventArgs.Empty);

        return ValueTask.CompletedTask;
    }
}
