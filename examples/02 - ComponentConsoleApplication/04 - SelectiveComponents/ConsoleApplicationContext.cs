using ConsoleApplication.Components.Host;
using ConsoleApplication.Components.Ops;
using CoreVar.CommandLineInterface;

namespace ConsoleApplication;

[Component<StartComponent>]
[Component<StopComponent>]
[Component<StatusComponent>]
partial class ConsoleApplicationContext : ComponentContext
{
}