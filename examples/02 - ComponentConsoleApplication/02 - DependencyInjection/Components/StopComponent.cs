using ConsoleApplication.Services;
using CoreVar.CommandLineInterface;
using Microsoft.Extensions.DependencyInjection;

namespace ConsoleApplication.Components;

[CommandName("stop")]
[Description("Stops the service")]
public class StopComponent(ServiceStopper serviceStopper) : CommandLineComponent
{

    // Services can also be setup by referencing the service collection
    public static void SetupServices(IServiceCollection services)
    {
        services.AddSingleton<ServiceStopper>();
    }

    public async ValueTask Execute()
    {
        serviceStopper.Stop();
        await Console.WriteLine("Host stopped.");
    }
}