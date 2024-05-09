using ConsoleApplication.Services;
using CoreVar.CommandLineInterface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ConsoleApplication.Components;

[CommandName("start")]
[Description("Starts the service")]
public class StartComponent(ServiceStarter serviceStarter) : CommandLineComponent
{

    public static void SetupHostBuilder(IHostApplicationBuilder hostBuilder)
    {
        hostBuilder.Services.AddSingleton<ServiceStarter>();
    }    

    // This allows you to interact with the host after the service provider has been built
    //public static void SetupHost(IHost host)
    //{

    //}

    public async ValueTask Execute()
    {
        serviceStarter.Start();
        await Console.WriteLine("Service started.");
    }

}
