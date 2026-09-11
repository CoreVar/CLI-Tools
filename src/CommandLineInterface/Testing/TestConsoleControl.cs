using System.Text;

namespace CoreVar.CommandLineInterface.Testing;

/// <summary>An in-memory console implementation for command tests.</summary>
public sealed class TestConsoleControl(IEnumerable<string>? input = null) : IConsoleControl
{
    private readonly Queue<string> _input = new(input ?? []);
    private readonly StringBuilder _output = new();
    private readonly StringBuilder _error = new();

    public string Output => _output.ToString();
    public string Error => _error.ToString();

    public ValueTask<string> ReadLine() => ValueTask.FromResult(_input.Count > 0 ? _input.Dequeue() : string.Empty);
    public ValueTask Write(string text) { _output.Append(text); return ValueTask.CompletedTask; }
    public ValueTask WriteLine(string text) { _output.AppendLine(text); return ValueTask.CompletedTask; }
    public ValueTask WriteErrorLine(string text) { _error.AppendLine(text); return ValueTask.CompletedTask; }
}
