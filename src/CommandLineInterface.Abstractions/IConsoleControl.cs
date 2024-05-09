using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface;

public interface IConsoleControl
{

    ValueTask Write(string text);

    ValueTask WriteLine(string text);

    ValueTask<string> ReadLine();


    ValueTask WriteErrorLine(string text);
}
