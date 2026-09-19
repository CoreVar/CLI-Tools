using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using CoreVar.CommandLineInterface.Services;

namespace CoreVar.CommandLineInterface;

public class BlazorConsoleControl : IConsoleControl, IConsoleInterruptSource, IBrowserTerminal
{
    private TaskCompletionSource<string> _readLineTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private CancellationTokenSource _interruptSource = new();
    private static ulong _key = 0;
    private static readonly Regex AnsiPattern = new("\\x1B\\[(?<codes>[0-9;]*)m", RegexOptions.CultureInvariant);
    private Color _foreground = Color.LightGray;
    private bool _isBold;
    private bool _isInputSecret;

    public event EventHandler? LinesChanged;
    public event EventHandler<TerminalSize>? SizeChanged;

    public static ulong NewKey()
        => Interlocked.Increment(ref _key);

    public List<ConsoleLine> Lines { get; } = [new ConsoleLine()];

    public int MaxLines { get; set; } = 2_000;

    public CancellationToken InterruptToken => _interruptSource.Token;
    public TerminalSize Size { get; private set; } = new(80, 24);
    public bool IsInputSecret
    {
        get => _isInputSecret;
        set { if (_isInputSecret == value) return; _isInputSecret = value; NotifyChanged(); }
    }

    public void Resize(int columns, int rows)
    {
        var size = new TerminalSize(Math.Max(1, columns), Math.Max(1, rows));
        if (size == Size) return;
        Size = size;
        SizeChanged?.Invoke(this, size);
    }

    public void SubmitLine(string text)
    {
        if (!IsInputSecret) ParseAndWrite(text);
        NewLine();

        var completionSource = _readLineTaskCompletionSource;

        _readLineTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        completionSource.TrySetResult(text);
        TrimScrollback();
        NotifyChanged();
    }

    public void CancelLine()
    {
        ParseAndWrite("^C", Color.IndianRed);
        NewLine();
        _interruptSource.Cancel();
        var completionSource = _readLineTaskCompletionSource;
        _readLineTaskCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        completionSource.TrySetResult(string.Empty);
        TrimScrollback();
        NotifyChanged();
    }

    public void Clear()
    {
        var current = Lines.Count == 0 ? new ConsoleLine() : Lines[^1];
        Lines.Clear();
        Lines.Add(current);
        NotifyChanged();
    }

    public void ShowCompletions(string input, IReadOnlyCollection<string> completions)
    {
        var prompt = Lines.Count == 0 ? [] : Lines[^1].Elements.Select(Clone).ToList();
        ParseAndWrite(input);
        NewLine();
        ParseAndWrite(string.Join("  ", completions.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)), Color.LightSkyBlue);
        NewLine();
        Lines[^1].Elements.AddRange(prompt);
        TrimScrollback();
        NotifyChanged();
    }

    public async ValueTask<string> ReadLine()
    {
        return await _readLineTaskCompletionSource.Task;
    }

    private void ParseAndWrite(string text, Color? color = null)
    {
        var position = 0;
        foreach (Match match in AnsiPattern.Matches(text))
        {
            WritePlain(text[position..match.Index], color);
            ApplyAnsi(match.Groups["codes"].Value);
            position = match.Index + match.Length;
        }
        WritePlain(text[position..], color);
    }

    private void WritePlain(string text, Color? overrideColor)
    {
        var buffer = new StringBuilder();
        void Flush()
        {
            if (buffer.Length == 0) return;
            var line = Lines[^1];
            var foreground = overrideColor ?? _foreground;
            var previous = line.Elements.LastOrDefault();
            if (previous is not null && previous.Color == foreground && previous.IsBold == _isBold)
                previous.Text += buffer.ToString();
            else
                line.Elements.Add(new ConsoleTextElement { Text = buffer.ToString(), Color = foreground, IsBold = _isBold });
            buffer.Clear();
        }

        foreach (var character in text)
        {
            switch (character)
            {
                case '\n':
                    Flush();
                    NewLine();
                    break;
                case '\r':
                    Flush();
                    Lines[^1].Elements.Clear();
                    break;
                case '\b':
                    Flush();
                    RemoveLastCharacter();
                    break;
                default:
                    if (!char.IsControl(character) || character == '\t') buffer.Append(character);
                    break;
            }
        }
        Flush();
    }

    private void RemoveLastCharacter()
    {
        var elements = Lines[^1].Elements;
        while (elements.Count > 0)
        {
            var last = elements[^1];
            if (last.Text.Length > 0)
            {
                last.Text = last.Text[..^1];
                if (last.Text.Length == 0) elements.RemoveAt(elements.Count - 1);
                return;
            }
            elements.RemoveAt(elements.Count - 1);
        }
    }

    private void ApplyAnsi(string codesText)
    {
        var codes = string.IsNullOrEmpty(codesText) ? [0] : codesText.Split(';').Select(value => int.TryParse(value, out var code) ? code : 0).ToArray();
        for (var index = 0; index < codes.Length; index++)
        {
            var code = codes[index];
            if (code == 0) { _foreground = Color.LightGray; _isBold = false; }
            else if (code == 1) _isBold = true;
            else if (code == 22) _isBold = false;
            else if (code == 39) _foreground = Color.LightGray;
            else if (code is >= 30 and <= 37) _foreground = BasicColor(code - 30, false);
            else if (code is >= 90 and <= 97) _foreground = BasicColor(code - 90, true);
            else if (code == 38 && index + 4 < codes.Length && codes[index + 1] == 2)
            {
                _foreground = Color.FromArgb(ClampByte(codes[index + 2]), ClampByte(codes[index + 3]), ClampByte(codes[index + 4]));
                index += 4;
            }
        }
    }

    private static int ClampByte(int value) => Math.Clamp(value, 0, 255);

    private static Color BasicColor(int index, bool bright) => (index, bright) switch
    {
        (0, false) => Color.Black,
        (1, false) => Color.Maroon,
        (2, false) => Color.Green,
        (3, false) => Color.Olive,
        (4, false) => Color.Navy,
        (5, false) => Color.Purple,
        (6, false) => Color.Teal,
        (7, false) => Color.Silver,
        (0, true) => Color.Gray,
        (1, true) => Color.Red,
        (2, true) => Color.Lime,
        (3, true) => Color.Yellow,
        (4, true) => Color.Blue,
        (5, true) => Color.Fuchsia,
        (6, true) => Color.Aqua,
        _ => Color.White
    };

    private void NewLine() => Lines.Add(new ConsoleLine());

    private static ConsoleTextElement Clone(ConsoleTextElement element) => new()
    {
        Text = element.Text,
        Color = element.Color,
        IsBold = element.IsBold
    };

    private void TrimScrollback()
    {
        var remove = Lines.Count - Math.Max(2, MaxLines);
        if (remove > 0) Lines.RemoveRange(0, remove);
    }

    private void NotifyChanged() => LinesChanged?.Invoke(this, EventArgs.Empty);

    public void ResetInterrupt()
    {
        if (!_interruptSource.IsCancellationRequested) return;
        _interruptSource.Dispose();
        _interruptSource = new CancellationTokenSource();
    }

    public ValueTask Write(string text)
    {
        ParseAndWrite(text);

        TrimScrollback();
        NotifyChanged();

        return ValueTask.CompletedTask;
    }

    public ValueTask WriteErrorLine(string text)
    {
        ParseAndWrite(text, Color.IndianRed);
        NewLine();

        TrimScrollback();
        NotifyChanged();

        return ValueTask.CompletedTask;
    }

    public ValueTask WriteLine(string text)
    {
        ParseAndWrite(text);
        NewLine();

        TrimScrollback();
        NotifyChanged();

        return ValueTask.CompletedTask;
    }
}
