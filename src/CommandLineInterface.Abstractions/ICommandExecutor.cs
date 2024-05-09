using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

public interface ICommandExecutor
{

    ValueTask<int> Execute(params string[] args);

    void EnqueueExecution(string[] args, Func<int, ValueTask>? callback = null);

}
