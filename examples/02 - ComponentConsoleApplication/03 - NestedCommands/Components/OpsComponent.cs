using ConsoleApplication.Components.Ops;
using CoreVar.CommandLineInterface;

namespace ConsoleApplication.Components;

[CommandName("ops")]
[Component<StatusComponent>]
public class OpsComponent : CommandLineComponent
{
}
