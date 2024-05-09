using ConsoleApplication.Components;
using CoreVar.CommandLineInterface;

namespace ConsoleApplication;

[Component<HostComponent>]
[Component<OpsComponent>]
partial class ConsoleApplicationContext : ComponentContext
{
}