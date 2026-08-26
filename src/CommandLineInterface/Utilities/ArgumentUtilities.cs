using System.Text;

namespace CoreVar.CommandLineInterface.Utilities;

public static class ArgumentUtilities
{

    public static string[] ParseArguments(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return [];

        var argList = new List<string>();
        var currentArg = new StringBuilder();
        var inQuotes = false;
        var argumentStarted = false;

        for (var index = 0; index < commandLine.Length; index++)
        {
            var c = commandLine[index];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                argumentStarted = true;
            }
            else if (c == '\\' && inQuotes && index + 1 < commandLine.Length &&
                (commandLine[index + 1] == '\\' || commandLine[index + 1] == '"'))
            {
                currentArg.Append(commandLine[++index]);
                argumentStarted = true;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (argumentStarted)
                {
                    argList.Add(currentArg.ToString());
                    currentArg.Clear();
                    argumentStarted = false;
                }
            }
            else
            {
                currentArg.Append(c);
                argumentStarted = true;
            }
        }

        if (inQuotes)
            throw new FormatException("The command line contains an unterminated quoted argument.");

        if (argumentStarted)
            argList.Add(currentArg.ToString());

        return [.. argList];
    }

    public static string ConvertToArgumentsString(string[] arguments)
    {
        var builder = new StringBuilder();
        foreach (var arg in arguments)
        {
            if (builder.Length > 0)
                builder.Append(' ');
            if (arg.Length == 0 || arg.Any(char.IsWhiteSpace) || arg.Contains('"'))
            {
                builder.Append('"');
                builder.Append(arg.Replace("\\", "\\\\").Replace("\"", "\\\""));
                builder.Append('"');
            }
            else
                builder.Append(arg);
        }
        return builder.ToString();
    }

}
