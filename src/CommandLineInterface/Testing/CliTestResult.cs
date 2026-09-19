namespace CoreVar.CommandLineInterface.Testing;

/// <summary>Captured result of an in-memory CLI execution.</summary>
public sealed record CliTestResult(int ExitCode, string Output, string Error);
