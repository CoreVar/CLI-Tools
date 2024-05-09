using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;

namespace CoreVar.CommandLineInterface.Support;

public class ApplicationContext(bool isReplMode, ICommandLineBuilder commandLineBuilder) : IApplicationContext, IApplicationContextInternals, IDisposable
{

    public bool IsReplMode { get; } = isReplMode;

    public bool IsShuttingDown { get; private set; }

    public void ShouldShutdown()
    {
        IsShuttingDown = true;
        RuntimeCancellationTokenSource.Cancel();
    }

    public void Dispose()
    {
        RuntimeCancellationTokenSource.Dispose();
    }

    public CancellationTokenSource RuntimeCancellationTokenSource { get; } = new();

    ICommandLineBuilder IApplicationContextInternals.CommandLineBuilder => commandLineBuilder;
}
