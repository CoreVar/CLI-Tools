using ConsoleApplication.Components.Host;
using CoreVar.CommandLineInterface;

namespace ConsoleApplication;

[Component<StartComponent>]
[Component<StopComponent>]
partial class HostContext : ComponentContext
{
}
