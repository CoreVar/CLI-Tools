namespace CliComponents.Components;

[CommandName("stop")]
public class StopComponent : CommandLineComponent
{

	public async ValueTask ExecuteAsync()
	{
		await Console.WriteLine("Stopping...");
	}

}
