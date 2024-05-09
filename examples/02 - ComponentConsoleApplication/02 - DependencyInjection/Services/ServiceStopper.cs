namespace ConsoleApplication.Services;

public class ServiceStopper(ServiceStatus serviceStatus)
{

    public void Stop()
    {
        serviceStatus.IsRunning = false;
    }

}
