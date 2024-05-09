using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CoreVar.CommandLineInterface.Utilities;

public static class ArgumentUtilities
{

    public static string[] ParseArguments(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return [];

        var argList = new List<string>();
        var currentArg = "";
        var inQuotes = false;
        var escape = false;

        foreach (var c in commandLine)
        {
            if (escape)
            {
                currentArg += c;
                escape = false;
            }
            else if (c == '\\')
                escape = true;
            else if (c == '\"')
                inQuotes = !inQuotes;
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (!string.IsNullOrEmpty(currentArg))
                {
                    argList.Add(currentArg);
                    currentArg = "";
                }
            }
            else
                currentArg += c;
        }

        if (!string.IsNullOrEmpty(currentArg))
            argList.Add(currentArg);

        return [.. argList];
    }

    public static string ConvertToArgumentsString(string[] arguments)
    {
        var builder = new StringBuilder();
        foreach (var arg in arguments)
        {
            if (builder.Length > 0)
                builder.Append(' ');
            builder.Append(arg);
        }
        return builder.ToString();
    }

}
