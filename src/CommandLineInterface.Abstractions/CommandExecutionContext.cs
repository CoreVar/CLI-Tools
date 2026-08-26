using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Runtime;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

public class CommandExecutionContext(IServiceProvider services, IConsoleControl console) : ICommandExecutionContextInternals
{
    private string[]? _arguments;
    private CommandTreeContext? _commandTreeContext;

    public string[] Arguments => _arguments!;

    public IServiceProvider Services => services;

    public int Result { get; set; }

    public IConsoleControl Console => console;

    /// <summary>Gets the token cancelled when the application is shutting down.</summary>
    public CancellationToken CancellationToken { get; private set; }

    public CommandTreeContext CommandTreeContext => _commandTreeContext ??= services.GetRequiredService<CommandTreeContext>();

    string[] ICommandExecutionContextInternals.Arguments { get => _arguments!; set => _arguments = value; }

    CancellationToken ICommandExecutionContextInternals.CancellationToken { get => CancellationToken; set => CancellationToken = value; }

}
