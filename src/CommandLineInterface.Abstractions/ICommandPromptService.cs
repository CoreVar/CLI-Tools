namespace CoreVar.CommandLineInterface;

/// <summary>A host-specific input channel, separate from command submission and history.</summary>
public interface ICommandPromptService
{
    bool IsInteractive { get; }

    /// <summary>Reads one value. Null means end of input; cancellation throws OperationCanceledException.
    /// Secret input must never be echoed, logged, or added to a transcript or history.</summary>
    ValueTask<string?> ReadAsync(CommandPromptRequest request, CancellationToken cancellationToken);
}

/// <summary>Contains presentation metadata only, never the answer.</summary>
public sealed class CommandPromptRequest(string label, bool isSecret = false)
{
    public string Label { get; } = label;
    public bool IsSecret { get; } = isSecret;
    /// <summary>Allows an empty answer for confirmation/default-value prompts. Metadata prompts reject empty answers.</summary>
    public bool AllowEmpty { get; init; }
}

/// <summary>A prompt could not supply a value. The message never contains an answer.</summary>
public sealed class CommandPromptException : Exception
{
    public CommandPromptException() : base("A required input value could not be read. Supply it explicitly or enable interactive prompting.") { }
}
