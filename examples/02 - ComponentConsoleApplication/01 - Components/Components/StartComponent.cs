using CoreVar.CommandLineInterface;

namespace ConsoleApplication.Components;

[CommandName("start")]
[Description("Starts the service")]
public class StartComponent : CommandLineComponent
{

    [CommandArgument("jobName")]
    [Description("My arg")]
    public string JobName { get; set; } = default!;

    [CommandOption("--delay")]
    public int? Delay { get; set; }

    [CommandOption("--log")]
    public bool Log { get; set; }

    public async ValueTask Execute()
    {
        if (Log)
            await Console.WriteLine("Logging is enabled");

        if (Delay.HasValue)
        {
            await Console.WriteLine($"Delaying startup by {Delay} seconds...");
            await Task.Delay(TimeSpan.FromSeconds(Delay.Value));
        }

        await Console.WriteLine($"Starting job '{JobName}'");
        await Console.WriteLine("Service started");
    }

}