using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Execution;

namespace CommandLineInterface.Tests;

[CollectionDefinition("Process exit code", DisableParallelization = true)]
public sealed class ProcessExitCodeCollection { }

[Collection("Process exit code")]
public sealed class CommandShutdownTests
{
    [Fact]
    public async Task Shutdown_waits_for_cancelled_command_cleanup_and_exit_code()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = CommandLineBuilder.Create();
        builder.Command("wait", command => command.OnExecute(
            new Func<CommandExecutionContext, ValueTask<int>>(async context =>
            {
                entered.SetResult();
                try { await Task.Delay(Timeout.Infinite, context.CancellationToken); }
                finally
                {
                    cleanupStarted.SetResult();
                    await releaseCleanup.Task;
                }
                return 0;
            })));
        var previousExitCode = Environment.ExitCode;
        await using var app = builder.Build(() => ["wait"]);
        try
        {
            await app.StartAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var stopping = app.StopAsync().AsTask();
            await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(stopping.IsCompleted, "Host stopped while command cleanup was still pending.");
            releaseCleanup.SetResult();
            await stopping.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(130, Environment.ExitCode);
        }
        finally
        {
            releaseCleanup.TrySetResult();
            await app.StopAsync();
            Environment.ExitCode = previousExitCode;
        }
    }
}
