namespace CoreVar.CommandLineInterface;

public interface IApplicationContext
{

    bool IsReplMode { get; }

    bool IsShuttingDown { get; }

    void ShouldShutdown();

    Func<string[]> ArgumentsRetriever { get; }

}
