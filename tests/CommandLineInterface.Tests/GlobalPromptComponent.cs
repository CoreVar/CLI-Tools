using CoreVar.CommandLineInterface;

[CommandName("global-prompt")]
public sealed class GlobalPromptComponent : CommandLineComponent
{
    public void Execute([CommandOption("--password", PromptIfMissing = true, Secret = true)] string password)
    {
        if (password != "synthetic-secret") throw new InvalidOperationException("Binding failed");
    }
}

[Component<GlobalPromptComponent>]
public partial class GlobalPromptContext : ComponentContext;
