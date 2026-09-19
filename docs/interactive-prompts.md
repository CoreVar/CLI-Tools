# Interactive prompts

Prompting is opt-in for each value. Existing argument and option metadata supports `PromptIfMissing`, `PromptLabel`, and `Secret`:

```csharp
[CommandOption("--password", PromptIfMissing = true, PromptLabel = "Password", Secret = true)]
public string? Password { get; set; }
```

The same metadata works on execute-method parameters and positional arguments. Fluent builders use:

```csharp
var password = command.Option<string>("--password")
    .PromptIfMissing("Password", secret: true);
```

The executor fills missing values before required-value validation and generated parameter/property binding. Explicit values, environment/configuration bindings, and non-null defaults bypass prompts. An explicitly present option without a value is a validation error, not a request to prompt. Prompted answers are stored in invocation binding state, never appended to original command arguments. Collection arguments receive one answer as one item; answers are not re-parsed as command text.

`Secret()` can mark an explicit value without enabling prompts. Commands declaring secret inputs are omitted from framework REPL history and executor echo. Secret defaults and completions are omitted from generated output; handler exception details are suppressed for sensitive executions. Applications and custom middleware must not log values themselves. OS command history and process argument inspection are outside this protection: prefer prompts, protected files, or explicit stdin for credentials rather than literal command-line passwords. Returned values are managed strings and cannot be guaranteed erased from memory; the native editable buffer is cleared after reading.

## Host policy and adapters

`CommandLineOptions.EnablePrompts = false` disables all framework prompts for a host. Batch callers can further restrict one invocation without changing shared policy:

```csharp
await executor.Execute(new CommandExecutionOptions { EnablePrompts = false }, args);
```

Applications may declare a boolean `--no-prompt` option. Its presence disables metadata prompts for that command. The framework does not reserve or silently strip that token. For a custom parser, enforce the scripting flag before calling the service.

Register an `ICommandPromptService` in the host's dependency injection container. The default `NativeCommandPromptService` only enables itself for a native console with interactive stdin, stdout, and stderr. Replacing `IConsoleControl` with a portal or test console does not grant prompt capability automatically.

```csharp
public interface ICommandPromptService
{
    bool IsInteractive { get; }
    ValueTask<string?> ReadAsync(CommandPromptRequest request, CancellationToken cancellationToken);
}
```

`CommandPromptRequest(string label, bool isSecret = false)` carries presentation metadata, never an answer. Adapters return null for EOF and throw `OperationCanceledException` for cancellation. Secret adapters must use a separate input channel with no echo, transcript, or command-history entry. The core package intentionally provides no browser password UI; portal hosts must implement and qualify their adapter before reporting `IsInteractive = true`.

The framework's `context.PromptAsync(new CommandPromptRequest("Password", isSecret: true), enabled: !noPrompt)` enforces host and invocation policy, interactive capability, cancellation, and nonempty input. It sanitizes adapter failures. Missing/disabled/empty input returns validation exit code 2 by default; cancellation returns 130. Existing string-based `PromptAsync` and `ConfirmAsync` helpers now use this same policy and adapter. Confirmation requests allow empty input for their default answer.

## Conditional credential sources and standalone consumers

When a command accepts password-file, password-stdin, and password options, resolve and validate those sources first. Call the shared service only when none was supplied. This avoids a premature metadata prompt before another credential source is considered.

Custom parsers outside `CommandExecutionContext` can construct the native implementation directly:

```csharp
var prompt = new CoreVar.CommandLineInterface.Support.NativeCommandPromptService();
if (noPrompt || !prompt.IsInteractive) throw new CommandPromptException();
var password = await prompt.ReadAsync(new CommandPromptRequest("Password", isSecret: true), cancellationToken);
if (string.IsNullOrEmpty(password)) throw new CommandPromptException();
```

Standalone callers own host policy and safe exception handling. The native reader supports backspace, Enter, Escape/Ctrl+C cancellation, Ctrl+D/Ctrl+Z EOF, and cancellation-token polling. Input is bounded to 4096 UTF-16 code units. Secret input has no character echo or masking count; ordinary input is echoed. Terminal control state and the input semaphore are restored after completion or failure.

## External modules

External modules receive redirected stdin. Without an explicit `ModuleInvocationContext.StandardInput` provider, stdin is closed. A child cannot implicitly inherit the terminal's interactive password channel. The ordinary line provider does not carry secret/no-echo metadata and must not be repurposed as a secure prompt protocol. A browser-to-subprocess prompt bridge needs a separately designed and qualified protocol.

## Local package qualification

The prompt packages are local development artifacts; no feed publication is implied. Consume matching Abstractions and Core versions using an externally supplied NuGet source directory plus NuGet.org for Microsoft dependencies. Keep an exact root version and a lockfile. Do not commit a machine-specific source path. Core includes the source generator, so no separate generator package is required.

Run `dotnet test tests/CommandLineInterface.Tests/CommandLineInterface.Tests.csproj` for framework qualification. Tests exercise generated metadata, binding, policy isolation, redaction, history, terminal editing, EOF, and cancellation. Real product terminal qualification is recorded separately by consuming applications.
