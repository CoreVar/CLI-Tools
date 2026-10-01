# Portable command files

Commands can read, write, check, and delete files through `Context.Files` (`CommandExecutionContext.Files` in fluent handlers). The same command code works on native and Blazor hosts. Native hosts use disk files; Blazor uses a bounded, session-local virtual filesystem. A missing Blazor read awaits user file selection without blocking the UI thread.

```csharp
[CommandName("import")]
public sealed class ImportComponent : CommandLineComponent
{
    public async Task ExecuteAsync(
        [CommandOption("--input", InputFile = true, PromptLabel = "Import document")] string input)
    {
        await using var stream = await Context.Files.OpenReadAsync(input);
        // Parse the stream here. No host-specific branch is needed.
    }
}
```

Run `import --input data.json` on either host. On native hosts, the file must exist on disk. On Blazor, `data.json` is a virtual cache key: if missing, the terminal shows a labelled file picker and waits. The selected file's bytes are assigned to the requested key; the selected filename does not have to match it.

`import` can omit the filename entirely. `InputFile = true` resolves it before the handler executes. Native interactive hosts ask for a disk path. Blazor assigns a stable key based on the command and input name and shows the picker. Subsequent executions reuse that cached file until it is deleted, cleared, or the session ends. The original command arguments and command history are not rewritten with inferred filenames or file contents.

`InputFile` works on string options and positional arguments, including component properties and execute-method parameters. The fluent equivalent is:

```csharp
var input = command.Option<string>("--input").InputFile("Import document");
command.OnExecute(async context =>
{
    await using var stream = await context.Files.OpenReadAsync(context.GetOption(input));
});
```

Use named options when several positional values could be omitted: the parser cannot infer whether a supplied positional token was intended for an earlier or later argument. An explicitly present option with no value remains a validation error. Environment, configuration, and default filename values are honored before requesting an inferred file.

## Files discovered during execution

Metadata is optional. A handler may discover another filename at runtime:

```csharp
await using var stream = await Context.Files.OpenReadAsync("references/catalog.json");
```

In Blazor, an uncached read shows the picker and suspends that execution until the user supplies the file or cancels. The executor continues to serialize commands, and the terminal disables command entry while the file request is pending. Other sessions remain independent. The picker is never opened automatically: the user chooses the file through the browser's normal file input.

The portable `ICommandFileSystem` also provides `ExistsAsync`, `WriteAllBytesAsync`, and `DeleteAsync`. Existence checks never prompt. Writes replace the named file; Blazor writes update the virtual cache and do not save to the user's disk. Generated files can be read by later commands. File contents are not added to terminal output or logs by this feature; handlers control their own output.

Direct `System.IO.File` calls are not intercepted. Use this API for portable operations. Returned streams are owned by the caller; dispose them and pass command cancellation to subsequent stream reads when appropriate.

## Blazor host setup

`AddBlazorConsoleControl()` registers `BlazorFileSystem` as the command host's `ICommandFileSystem`. `CommandLineConsole` finds that provider and renders `CommandFilePicker` automatically. Create a separate `CliApp` per terminal/user session. Never share a CLI host or its cache across users.

For a custom `IBrowserTerminalSession`, expose its file provider through `Files`, pass the same provider to `CommandLineConsole.FileSystem`, or render `<CommandFilePicker Files="files" />` separately. Register that exact instance as `ICommandFileSystem` in the command host. A read with no visible attached picker fails promptly instead of waiting forever.

The default cache limits are 10 MiB per file, 50 MiB total, and 100 files. Override them in the individual CLI host before calling `AddBlazorConsoleControl`:

```csharp
host.Services.AddSingleton(new BlazorFileSystemOptions
{
    MaxFileBytes = 5 * 1024 * 1024,
    MaxCacheBytes = 20 * 1024 * 1024,
    MaxCachedFiles = 40
});
host.AddBlazorConsoleControl();
```

Actual streamed bytes are bounded; browser-reported file size alone is not trusted. Incomplete, cancelled, oversized, or failed uploads are not cached. A failed upload leaves the request available for retry or cancellation. The cache also limits empty-file counts and virtual path lengths. Virtual paths are case-sensitive; slash separators are normalized, and drive names and parent traversal are rejected. Blazor never resolves these keys against the server's disk.

The cache is in memory, not IndexedDB or browser local storage. For Blazor Server, selected bytes are uploaded to memory in that user's server-side CLI session; for WebAssembly, they remain in the client runtime. Cache lifetime follows the CLI host. Removing the picker cancels its outstanding request; disposing the host clears cached files. Server disconnect cleanup follows the application's circuit/session lifetime, including any reconnection retention period.

## Cancellation and automation

The panel provides **Cancel command** and supports Escape/Ctrl+C while focused. Cancellation tokens, picker removal, and host disposal also cancel pending reads. No partial file is committed. Cache hits continue to work without interactive permission.

`Context.Files` applies the existing host `CommandLineOptions.EnablePrompts` and per-invocation `CommandExecutionOptions.EnablePrompts` policy. A declared boolean `--no-prompt` option also disables file requests. Batch execution fails promptly for uncached files; it never waits for a user picker. Code resolving `ICommandFileSystem` directly owns its interaction policy and cancellation and should pass `AllowInteraction = false` when needed:

```csharp
await files.OpenReadAsync(new CommandFileRequest("input.json") { AllowInteraction = false }, token);
```

## Validation

The framework tests cover native operations, inferred filename binding, cache reuse and isolation, concurrent requests, cancellation, failed uploads, quotas, and batch policy. `tests/browser/file-picker.test.mjs` runs a real interactive Blazor fixture at mobile and desktop widths and exercises selection, cancellation, retry, cache reuse, and session isolation. Run `pnpm --dir tests/browser test` after installing Playwright's Chromium, or set `BROWSER_CHANNEL=msedge` to use installed Edge.
