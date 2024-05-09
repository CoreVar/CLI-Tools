using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Support;

public class NativeConsoleControl : IConsoleControl
{
    public ValueTask<string> ReadLine()
        => ValueTask.FromResult(Console.ReadLine())!;

    public ValueTask Write(string text)
    {
        Console.Write(text);
        return ValueTask.CompletedTask;
    }

    public ValueTask WriteLine(string text)
    {
        Console.WriteLine(text);
        return ValueTask.CompletedTask;
    }

    public ValueTask WriteErrorLine(string text)
    {
        Console.Error.WriteLine(text);
        return ValueTask.CompletedTask;
    }
}
