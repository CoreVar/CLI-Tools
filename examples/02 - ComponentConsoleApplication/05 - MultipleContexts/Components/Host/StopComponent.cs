using CoreVar.CommandLineInterface;

namespace ConsoleApplication.Components.Host;

[CommandName("stop")]
[Description("Stops the service")]
public class StopComponent : CommandLineComponent
{
    public async ValueTask Execute()
    {
        await Console.WriteLine("Host stopped.");
    }
}