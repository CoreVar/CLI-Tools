using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

public interface ICommandExecutor
{

    ValueTask<int> Execute(params string[] args);

    /// <summary>Executes with invocation-specific interaction policy. Unsupported implementations fail closed.</summary>
    ValueTask<int> Execute(CommandExecutionOptions options, params string[] args)
        => throw new NotSupportedException("This executor does not support per-invocation policy.");

    void EnqueueExecution(string[] args, Func<int, ValueTask>? callback = null);

}
