namespace CoreVar.CommandLineInterface.Validation;

/// <summary>Represents the result of validating a command value or invocation.</summary>
public readonly record struct ValidationResult(bool IsValid, string? Message)
{
    public static ValidationResult Success { get; } = new(true, null);

    public static ValidationResult Error(string message) => new(false, message);
}
