using System.Text;
using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Support;
using CoreVar.CommandLineInterface.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CommandLineInterface.Tests;

[CollectionDefinition("Prompt host tests", DisableParallelization = true)]
public sealed class PromptHostCollection;

[Collection("Prompt host tests")]
public sealed class PromptTests
{
    private static CommandLineBuilder Builder(ICommandPromptService prompt, bool enabled = true)
    {
        var builder = CommandLineBuilder.Create("test", new CommandLineOptions { EnablePrompts = enabled });
        builder.SetupHostBuilder(host => host.Services.AddSingleton(prompt));
        return builder;
    }

    [Fact]
    public async Task Missing_values_bind_without_modifying_argv()
    {
        var prompt = new FakePrompt("secret-value", "alice");
        var builder = Builder(prompt);
        var password = builder.Option<string>("--password").IsRequired().PromptIfMissing("Password", true);
        var user = builder.Argument<string>("user").PromptIfMissing("User");
        builder.OnExecute(context =>
        {
            Assert.Equal("secret-value", context.GetOption(password));
            Assert.Equal("alice", context.GetArgument(user));
            Assert.Empty(context.Arguments);
            return ValueTask.CompletedTask;
        });
        var result = await builder.TestAsync("");
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(new[] { "Password", "User" }, prompt.Requests.Select(r => r.Label));
        Assert.True(prompt.Requests[0].IsSecret);
        Assert.DoesNotContain("secret-value", result.Output + result.Error);
    }

    [Fact]
    public async Task Explicit_value_bypasses_prompt_and_redacts_handler_error()
    {
        var prompt = new FakePrompt();
        var builder = Builder(prompt);
        var password = builder.Option<string>("--password").PromptIfMissing("Password", true);
        builder.OnExecute(context => ValueTask.FromException(new InvalidOperationException(context.GetOption(password))));
        var result = await builder.TestAsync("--password secret-value");
        Assert.Empty(prompt.Requests);
        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("secret-value", result.Output + result.Error);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Disabled_or_noninteractive_fails_without_reading(bool enabled, bool interactive)
    {
        var prompt = new FakePrompt("secret-value") { IsInteractive = interactive };
        var builder = Builder(prompt, enabled);
        builder.Option<string>("--password").PromptIfMissing("Password", true);
        builder.OnExecute(_ => ValueTask.FromException(new Exception("Handler must not run")));
        Assert.Equal(2, (await builder.TestAsync("")).ExitCode);
        Assert.Empty(prompt.Requests);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Empty_or_eof_fails_safely(string? answer)
    {
        var builder = Builder(new FakePrompt(answer));
        builder.Option<string>("--password").PromptIfMissing("Password", true);
        builder.OnExecute(_ => ValueTask.FromException(new Exception("Handler must not run")));
        Assert.Equal(2, (await builder.TestAsync("")).ExitCode);
    }

    [Fact]
    public async Task Cancellation_returns_130()
    {
        var builder = Builder(new FakePrompt { Cancel = true });
        builder.Option<string>("--password").PromptIfMissing("Password", true);
        builder.OnExecute(_ => ValueTask.CompletedTask);
        Assert.Equal(130, (await builder.TestAsync("")).ExitCode);
    }

    [Theory]
    [InlineData("--help", 0)]
    [InlineData("--no-prompt", 2)]
    public async Task Help_and_scripting_flag_do_not_read(string input, int code)
    {
        var prompt = new FakePrompt();
        var builder = Builder(prompt);
        builder.Option<bool>("--no-prompt");
        builder.Option<string>("--password").PromptIfMissing("Password", true);
        builder.OnExecute(_ => ValueTask.CompletedTask);
        Assert.Equal(code, (await builder.TestAsync(input)).ExitCode);
        Assert.Empty(prompt.Requests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Batch_gate_is_per_invocation_and_explicit_secrets_are_not_echoed(bool repl)
    {
        var prompt = new FakePrompt("secret-value");
        var builder = Builder(prompt);
        builder.EnableRepl(repl);
        var console = new TestConsoleControl();
        builder.SetupHostBuilder(host => host.Services.AddSingleton<IConsoleControl>(console));
        builder.Command("connect", command =>
        {
            command.Option<string>("--password").PromptIfMissing("Password", true);
            command.OnExecute(_ => ValueTask.CompletedTask);
        });
        await using var app = builder.Build(() => []);
        var executor = app.Host.Services.GetRequiredService<ICommandExecutor>();
        Assert.Equal(2, await executor.Execute(new CommandExecutionOptions { EnablePrompts = false }, "connect"));
        Assert.Empty(prompt.Requests);
        Assert.Equal(0, await executor.Execute("connect"));
        Assert.Single(prompt.Requests);
        Assert.Equal(0, await executor.Execute("connect", "--password", "explicit-secret"));
        Assert.DoesNotContain("explicit-secret", console.Output + console.Error);
    }

    [Fact]
    public async Task Native_secret_backspace_is_not_echoed()
    {
        var terminal = new FakeTerminal(Key('a'), Key('b'), new('\b', ConsoleKey.Backspace, false, false, false), Key('c'), new('\r', ConsoleKey.Enter, false, false, false));
        var prompt = new NativeCommandPromptService(new NativeConsoleControl(), terminal);
        Assert.Equal("ac", await prompt.ReadAsync(new("Password", true), default));
        Assert.Equal("Password: " + Environment.NewLine, terminal.Output.ToString());
        Assert.False(terminal.TreatControlCAsInput);
    }

    [Fact]
    public async Task Native_cancellation_restores_terminal_and_releases_lock()
    {
        var terminal = new FakeTerminal();
        var prompt = new NativeCommandPromptService(new NativeConsoleControl(), terminal);
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => prompt.ReadAsync(new("Password", true), cancel.Token).AsTask());
        Assert.False(terminal.TreatControlCAsInput);
        terminal.Keys.Enqueue(new('\r', ConsoleKey.Enter, false, false, false));
        Assert.Equal("", await prompt.ReadAsync(new("Password", true), default));
    }

    [Fact]
    public async Task Native_refuses_noninteractive_or_portal_console()
    {
        var terminal = new FakeTerminal { IsInteractive = false };
        var prompt = new NativeCommandPromptService(new NativeConsoleControl(), terminal);
        await Assert.ThrowsAsync<CommandPromptException>(() => prompt.ReadAsync(new("Password", true), default).AsTask());
        Assert.Empty(terminal.Output.ToString());
        Assert.False(new NativeCommandPromptService(new TestConsoleControl()).IsInteractive);
    }

    private static ConsoleKeyInfo Key(char c) => new(c, ConsoleKey.A, false, false, false);

    [Fact]
    public async Task Generated_global_context_and_void_handler_bind_prompt_answer()
    {
        var builder = Builder(new FakePrompt("synthetic-secret"));
        builder.Components<GlobalPromptContext>();
        Assert.Equal(0, (await builder.TestAsync("global-prompt")).ExitCode);
    }

    [Fact]
    public async Task Secret_commands_are_excluded_from_repl_history()
    {
        var builder = Builder(new FakePrompt());
        builder.EnableRepl();
        builder.Command("connect", command =>
        {
            command.Option<string>("--password").Secret();
            command.OnExecute(_ => ValueTask.CompletedTask);
        });
        var result = await builder.TestAsync("", ["connect --password secret-value", "history", "!!", "exit"]);
        Assert.DoesNotContain("secret-value", result.Output + result.Error);
        Assert.Contains("No commands in history", result.Error);
    }

    [Fact]
    public async Task Secret_defaults_are_not_in_help_documentation_or_completions()
    {
        var builder = Builder(new FakePrompt());
        builder.Option<string>("--password").Default("secret-value").Complete("secret-value").Secret();
        builder.OnExecute(_ => ValueTask.CompletedTask);
        var result = await builder.TestAsync("--help");
        Assert.DoesNotContain("secret-value", result.Output + result.Error);
        await using var app = builder.Build(() => []);
        Assert.DoesNotContain("secret-value", app.GenerateMarkdown() + app.GenerateCompletion("bash"));
    }

    [Theory]
    [InlineData(ConsoleKey.C)]
    [InlineData(ConsoleKey.Escape)]
    public async Task Native_cancel_keys_restore_terminal(ConsoleKey key)
    {
        var terminal = new FakeTerminal(new ConsoleKeyInfo('\0', key, false, false, key == ConsoleKey.C));
        var prompt = new NativeCommandPromptService(new NativeConsoleControl(), terminal);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => prompt.ReadAsync(new("Password", true), default).AsTask());
        Assert.False(terminal.TreatControlCAsInput);
    }

    [Theory]
    [InlineData(ConsoleKey.D)]
    [InlineData(ConsoleKey.Z)]
    public async Task Native_eof_returns_null(ConsoleKey key)
    {
        var terminal = new FakeTerminal(new ConsoleKeyInfo('\0', key, false, false, true));
        Assert.Null(await new NativeCommandPromptService(new NativeConsoleControl(), terminal).ReadAsync(new("Password", true), default));
    }
    private sealed class FakeTerminal(params ConsoleKeyInfo[] keys) : IPromptTerminal
    {
        public Queue<ConsoleKeyInfo> Keys { get; } = new(keys);
        public StringBuilder Output { get; } = new();
        public bool IsInteractive { get; set; } = true;
        public bool TreatControlCAsInput { get; set; }
        public bool KeyAvailable => Keys.Count > 0;
        public ConsoleKeyInfo ReadKey() => Keys.Dequeue();
        public void Write(string text) => Output.Append(text);
    }
    private sealed class FakePrompt(params string?[] answers) : ICommandPromptService
    {
        public Queue<string?> Answers { get; } = new(answers);
        public List<CommandPromptRequest> Requests { get; } = [];
        public bool IsInteractive { get; set; } = true;
        public bool Cancel { get; init; }
        public ValueTask<string?> ReadAsync(CommandPromptRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Cancel) throw new OperationCanceledException();
            return ValueTask.FromResult(Answers.Dequeue());
        }
    }
}
