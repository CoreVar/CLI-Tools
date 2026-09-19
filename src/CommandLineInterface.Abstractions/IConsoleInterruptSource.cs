namespace CoreVar.CommandLineInterface;

/// <summary>Exposes user-initiated terminal interrupts such as Ctrl+C.</summary>
public interface IConsoleInterruptSource
{
    /// <summary>Gets a token cancelled by the next user interrupt.</summary>
    CancellationToken InterruptToken { get; }

    /// <summary>Prepares the source to receive the next interrupt.</summary>
    void ResetInterrupt();
}
