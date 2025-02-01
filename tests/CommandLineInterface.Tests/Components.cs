using CoreVar.CommandLineInterface;

namespace CommandLineInterface.Tests;

public class Components
{
    [Fact]
    public async Task ManyArguments()
    {
        await CliApp.RunAsync(cliBuilder =>
        {
            cliBuilder
                .EnableRepl()
                .SetupHostBuilder(hostBuilder =>
                {

                })
                .Components<ManyArgumentsComponentContext>();
        }, ["manyargs", "/var/www/coremq/.env", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10"]);
    }
}

[CommandName("manyargs")]
public class ManyArgumentsComponent : CommandLineComponent
{

    [CommandArgument("environment-file")]
    public string EnvironmentFile { get; set; } = default!;

    [CommandArgument("ip-address")]
    public string IpAddress { get; set; } = default!;

    [CommandArgument("registration-code")]
    public string RegistrationCode { get; set; } = default!;

    [CommandArgument("location")]
    public string Location { get; set; } = default!;

    [CommandArgument("subscription-id")]
    public string SubscriptionId { get; set; } = default!;

    [CommandArgument("subscription-identifier")]
    public string SubscriptionIdentifier { get; set; } = default!;

    [CommandArgument("subscription-name")]
    public string SubscriptionName { get; set; } = default!;

    [CommandArgument("subscription-tenant-id")]
    public string SubscriptionTenantId { get; set; } = default!;

    [CommandArgument("resource-group-name")]
    public string ResourceGroupName { get; set; } = default!;

    [CommandArgument("resource-group-location")]
    public string ResourceGroupLocation { get; set; } = default!;

    [CommandArgument("is-resource-group-new")]
    public bool IsResourceGroupNew { get; set; } = default!;

    public async Task ExecuteAsync()
    {
        await Console.WriteLine($"EnvironmentFile: {EnvironmentFile}");
        await Console.WriteLine($"IpAddress: {IpAddress}");
    }
}

[Component<ManyArgumentsComponent>]
public partial class ManyArgumentsComponentContext : ComponentContext
{

}
