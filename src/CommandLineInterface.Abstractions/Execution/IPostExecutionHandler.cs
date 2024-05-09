namespace CoreVar.CommandLineInterface.Execution;

public interface IPostExecutionHandler
{

    ValueTask Terminate();

}
