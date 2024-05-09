namespace BlazorApplication.Client.Services;

public class GreetingService
{

    public event EventHandler? GreetingChanged;

    public void SetGreeting(string text)
    {
        Greeting = text;
        GreetingChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Greeting { get; private set; } = "Hello, world!";

}
