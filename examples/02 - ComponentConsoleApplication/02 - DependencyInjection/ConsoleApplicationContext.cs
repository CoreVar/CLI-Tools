using ConsoleApplication.Components;
using CoreVar.CommandLineInterface;

namespace ConsoleApplication;

[Component<StartComponent>]
[Component<StopComponent>]
partial class ConsoleApplicationContext : ComponentContext
{
}

