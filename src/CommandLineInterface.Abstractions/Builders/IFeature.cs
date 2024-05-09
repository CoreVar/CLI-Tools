namespace CoreVar.CommandLineInterface.Builders;

public interface IFeature
{

    ValueTask Activate(CommandExecutionContext context);

    ValueTask Terminate(CommandExecutionContext context);

}