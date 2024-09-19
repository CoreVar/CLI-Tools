namespace CliComponents.Components;

[CommandName("stop")]
public class StopComponent : CommandLineComponent
{

	public async ValueTask Execute()
	{
		await Console.WriteLine("Stopping...");
	}

}
