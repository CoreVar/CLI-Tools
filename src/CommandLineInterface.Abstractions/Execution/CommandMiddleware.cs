namespace CoreVar.CommandLineInterface.Execution;

/// <summary>Represents the next operation in a command execution pipeline.</summary>
public delegate ValueTask CommandExecutionDelegate(CommandExecutionContext context);

/// <summary>Wraps command execution with cross-cutting behavior.</summary>
public delegate ValueTask CommandMiddleware(CommandExecutionContext context, CommandExecutionDelegate next);
