using ConsoleApplication.Components.Host;
using CoreVar.CommandLineInterface;

namespace ConsoleApplication.Components;

[CommandName("host")]
[NestedCommand<StartComponent>]
[NestedCommand<StopComponent>]
public class HostComponent : CommandLineComponent
{
}
