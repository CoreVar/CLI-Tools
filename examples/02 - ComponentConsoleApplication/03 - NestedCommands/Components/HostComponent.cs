using ConsoleApplication.Components.Host;
using CoreVar.CommandLineInterface;

namespace ConsoleApplication.Components;

[CommandName("host")]
[Component<StartComponent>]
[Component<StopComponent>]
public class HostComponent : CommandLineComponent
{
}
