namespace CoreVar.CommandLineInterface;

/// <summary>Policy for one execution; does not mutate shared host options.</summary>
public sealed class CommandExecutionOptions
{
    public bool EnablePrompts { get; init; } = true;
}
