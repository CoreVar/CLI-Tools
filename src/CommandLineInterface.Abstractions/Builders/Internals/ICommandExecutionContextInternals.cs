namespace CoreVar.CommandLineInterface.Builders.Internals;

public interface ICommandExecutionContextInternals
{

    string[] Arguments { get; set; }

    CancellationToken CancellationToken { get; set; }

}
