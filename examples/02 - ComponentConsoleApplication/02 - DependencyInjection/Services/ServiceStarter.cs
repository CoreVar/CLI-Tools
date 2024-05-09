namespace ConsoleApplication.Services;

public class ServiceStarter(ServiceStatus serviceStatus)
{

    public void Start()
    {
        serviceStatus.IsRunning = true;
    }

}
