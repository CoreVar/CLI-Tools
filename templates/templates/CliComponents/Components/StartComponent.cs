namespace CliComponents.Components;

[CommandName("start")]
public class StartComponent : CommandLineComponent
{

	public async ValueTask ExecuteAsync()
	{
		await Console.WriteLine("Starting...");
	}

}
