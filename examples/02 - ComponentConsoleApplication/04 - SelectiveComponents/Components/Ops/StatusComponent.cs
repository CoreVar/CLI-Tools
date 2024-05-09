using CoreVar.CommandLineInterface;

namespace ConsoleApplication.Components.Ops;

[CommandName("status")]
public class StatusComponent : CommandLineComponent
{

    public async ValueTask Execute()
    {
        await Console.WriteLine("Status is good.");
    }

}
