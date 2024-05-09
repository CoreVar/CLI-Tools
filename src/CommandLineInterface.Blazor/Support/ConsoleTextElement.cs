using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Services;

public sealed class ConsoleTextElement
{

    public ulong Key { get; } = BlazorConsoleControl.NewKey();

    public Color Color { get; set; }

    public string Text { get; set; } = string.Empty;

}
