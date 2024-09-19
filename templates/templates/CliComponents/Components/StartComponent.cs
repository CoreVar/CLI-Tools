namespace CliComponents.Components;

[CommandName("start")]
public class StartComponent : CommandLineComponent
{

	public async ValueTask Execute()
	{
		await Console.WriteLine("Starting...");
	}

}
