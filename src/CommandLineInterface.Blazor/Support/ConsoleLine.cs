using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Services;

public sealed class ConsoleLine
{

    public ulong Key { get; } = BlazorConsoleControl.NewKey();

    public List<ConsoleTextElement> Elements { get; } = [];

}
