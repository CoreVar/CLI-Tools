using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Support;
using CoreVar.CommandLineInterface.Testing;
using Microsoft.Extensions.DependencyInjection;
var builder = CommandLineBuilder.Create("consumer");
builder.Components<PromptContext>();
builder.SetupHostBuilder(host => host.Services.AddSingleton<ICommandPromptService, FakePrompt>());
var result = await builder.TestAsync("check");
if(result.ExitCode != 0 || result.Output.Contains("synthetic-secret") || result.Error.Contains("synthetic-secret")) throw new Exception("Prompt consumer failed");
if(new NativeCommandPromptService().IsInteractive) throw new Exception("Redirected consumer reported interactive");
Console.WriteLine("prompt-consumer-passed");
[CommandName("check")]
public class PromptComponent : CommandLineComponent {
 public void Execute([CommandOption("--password", PromptIfMissing=true, PromptLabel="Password", Secret=true)] string password) {
  if(password != "synthetic-secret") throw new Exception("Binding failed");
 }
}
[Component<PromptComponent>]
public partial class PromptContext : ComponentContext;
public class FakePrompt : ICommandPromptService {
 public bool IsInteractive => true;
 public ValueTask<string?> ReadAsync(CommandPromptRequest request, CancellationToken cancellationToken) {
  if(!request.IsSecret || request.Label!="Password") throw new Exception("Metadata failed");
  return ValueTask.FromResult<string?>("synthetic-secret");
 }
}
