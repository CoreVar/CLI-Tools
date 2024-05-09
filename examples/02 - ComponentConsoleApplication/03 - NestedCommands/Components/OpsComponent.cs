using ConsoleApplication.Components.Ops;
using CoreVar.CommandLineInterface;

namespace ConsoleApplication.Components;

[CommandName("ops")]
[NestedCommand<StatusComponent>]
public class OpsComponent : CommandLineComponent
{
}
