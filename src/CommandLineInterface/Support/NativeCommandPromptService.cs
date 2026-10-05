namespace CoreVar.CommandLineInterface.Support;

/// <summary>Native terminal prompts. Redirected streams and non-native console hosts fail closed.</summary>
public sealed class NativeCommandPromptService : ICommandPromptService
{
    private static readonly SemaphoreSlim InputLock = new(1, 1);
    private readonly IConsoleControl console;
    private readonly IPromptTerminal terminal;

    public NativeCommandPromptService() : this(new NativeConsoleControl()) { }
    public NativeCommandPromptService(IConsoleControl console) : this(console, new PromptTerminal()) { }
    internal NativeCommandPromptService(IConsoleControl console, IPromptTerminal terminal)
    {
        this.console = console;
        this.terminal = terminal;
    }

    public bool IsInteractive => console is NativeConsoleControl && terminal.IsInteractive;

    public async ValueTask<string?> ReadAsync(CommandPromptRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsInteractive) throw new CommandPromptException();
        await InputLock.WaitAsync(cancellationToken);
        var buffer = new char[4096];
        var length = 0;
        var previousControlC = false;
        var controlCChanged = false;
        try
        {
            if (!IsInteractive) throw new CommandPromptException();
            previousControlC = terminal.TreatControlCAsInput;
            terminal.TreatControlCAsInput = true;
            controlCChanged = true;
            terminal.Write(request.Label + ": ");
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!terminal.KeyAvailable)
                {
                    await Task.Delay(20, cancellationToken);
                    continue;
                }
                var key = terminal.ReadKey();
                if (key.Key == ConsoleKey.Escape || (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control)))
                    throw new OperationCanceledException(cancellationToken);
                if ((key.Key is ConsoleKey.D or ConsoleKey.Z) && key.Modifiers.HasFlag(ConsoleModifiers.Control))
                    return null;
                if (key.Key == ConsoleKey.Enter) return new string(buffer, 0, length);
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (length > 0)
                    {
                        buffer[--length] = '\0';
                        if (!request.IsSecret) terminal.Write("\b \b");
                    }
                    continue;
                }
                if (char.IsControl(key.KeyChar)) continue;
                if (length == buffer.Length) throw new CommandPromptException();
                buffer[length++] = key.KeyChar;
                if (!request.IsSecret) terminal.Write(key.KeyChar);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { throw new CommandPromptException(); }
        finally
        {
            Array.Clear(buffer);
            try
            {
                if (controlCChanged) terminal.TreatControlCAsInput = previousControlC;
                terminal.Write(Environment.NewLine);
            }
            finally { InputLock.Release(); }
        }
    }
}

internal interface IPromptTerminal
{
    bool IsInteractive { get; }
    bool TreatControlCAsInput { get; set; }
    bool KeyAvailable { get; }
    ConsoleKeyInfo ReadKey();
    void Write(string text);
    void Write(char character) => Write(character.ToString());
}

internal sealed class PromptTerminal : IPromptTerminal
{
    public bool IsInteractive => Environment.UserInteractive && !Console.IsInputRedirected &&
        !Console.IsOutputRedirected && !Console.IsErrorRedirected;
    public bool TreatControlCAsInput { get => Console.TreatControlCAsInput; set => Console.TreatControlCAsInput = value; }
    public bool KeyAvailable => Console.KeyAvailable;
    public ConsoleKeyInfo ReadKey() => Console.ReadKey(intercept: true);
    public void Write(string text) => Console.Error.Write(text);
}
