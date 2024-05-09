using CoreVar.CommandLineInterface.Interfaces;
using CoreVar.CommandLineInterface.Runtime;
using CoreVar.CommandLineInterface.Utilities;
using System.Text;

namespace CoreVar.CommandLineInterface.Support;

public class HelpExecutor(IServiceProvider serviceProvider, IApplicationContext appContext, IConsoleControl console) : IHelpExecutor
{
    public async ValueTask ShowHelp(CommandTreeContext context)
    {
        var outputBuilder = new StringBuilder();
        var executingCommand = CommandTreeHelpers.GetExecutingCommand(context.Root);

        if (executingCommand.Element.Description is not null)
        {
            outputBuilder.AppendLine(executingCommand.Element.Description);
            outputBuilder.AppendLine(string.Empty);
        }

        if (executingCommand.Element.ExecuteDelegate is not null)
        {
            outputBuilder.AppendLine("Usage:");

            if (executingCommand.Element.Usages is null)
                WriteUsage(outputBuilder, context);
            else
                foreach (var usage in executingCommand.Element.Usages)
                {
                    outputBuilder.AppendLine(usage.Usage(serviceProvider));
                    outputBuilder.AppendLine(string.Empty);

                    if (usage.Description is not null)
                    {
                        outputBuilder.AppendLine(usage.Description);
                        outputBuilder.AppendLine(string.Empty);
                    }
                }

            outputBuilder.AppendLine(string.Empty);
        }

        int maxHeaderLength = 0;

        if (executingCommand.Element.Arguments?.Count > 0)
            maxHeaderLength = Math.Max(maxHeaderLength, executingCommand.Element.Arguments.Max(a => a.Name.Length));

        if (executingCommand.Element.Options?.Count > 0)
            maxHeaderLength = Math.Max(maxHeaderLength, executingCommand.Element.Options.Max(o =>
            {
                if (o.Value.Aliases?.Count > 0)
                    return o.Value.Name.Length + o.Value.Aliases.Sum(a => a.Length + 1);
                else
                    return o.Value.Name.Length;
            }));

        if (executingCommand.Element.Children.Count > 0)
        {
            maxHeaderLength = Math.Max(maxHeaderLength, executingCommand.Element.Children.Max(e => e.Value.Name.Length));
        
            outputBuilder.AppendLine("Commands:");
            foreach (var child in executingCommand.Element.Children)
            {
                outputBuilder.Append(new string(' ', 4));
                outputBuilder.Append(child.Value.Name);
                if (child.Value.Description is not null)
                {
                    outputBuilder.Append(new string(' ', maxHeaderLength - child.Value.Name.Length + 4));
                    outputBuilder.AppendLine(child.Value.Description);
                }
                else
                    outputBuilder.AppendLine(string.Empty);
            }

            outputBuilder.AppendLine(string.Empty);
        }

        if (executingCommand.Element.Arguments?.Count > 0)
        {
            outputBuilder.AppendLine("Arguments:");

            foreach (var argument in executingCommand.Element.Arguments)
            {
                outputBuilder.Append(new string(' ', 4));
                outputBuilder.Append(argument.Name);
                if (argument.Description is not null)
                {
                    outputBuilder.Append(new string(' ', maxHeaderLength - argument.Name.Length + 4));
                    outputBuilder.AppendLine(argument.Description);
                }
                else
                    outputBuilder.AppendLine(string.Empty);
            }

            outputBuilder.AppendLine(string.Empty);
        }

        if (executingCommand.Element.Options?.Count > 0)
        {
            outputBuilder.AppendLine("Options:");

            foreach (var option in executingCommand.Element.Options)
            {
                outputBuilder.Append(new string(' ', 4));
                outputBuilder.Append(option.Value.Name);
                var headerLength = option.Value.Name.Length;
                if (option.Value.Aliases?.Count > 0)
                {
                    foreach (string alias in option.Value.Aliases)
                    {
                        outputBuilder.Append("|");
                        outputBuilder.Append(alias);
                        headerLength += alias.Length + 1;
                    }
                }
                if (option.Value.Description is not null)
                {
                    outputBuilder.Append(new string(' ', maxHeaderLength - headerLength + 4));
                    outputBuilder.AppendLine(option.Value.Description);
                }
                else
                    outputBuilder.AppendLine(string.Empty);
            }

            outputBuilder.AppendLine(string.Empty);
        }

        await console.Write(outputBuilder.ToString());
    }

    private void WriteUsage(StringBuilder outputBuilder, CommandTreeContext context)
    {
        var usage = new StringBuilder();

        if (!appContext.IsReplMode)
            usage.Append(context.Root.Element.Name);

        var currentElementContext = context.Root;
        var executingElementContext = currentElementContext;
        while ((currentElementContext = currentElementContext!.Child) is not null)
        {
            if (usage.Length > 0)
                usage.Append(' ');
            usage.Append(currentElementContext.Element.Name);

            executingElementContext = currentElementContext;
        }

        if (executingElementContext.Element.Arguments is not null)
        {
            var isOptional = false;
            foreach (var argument in executingElementContext.Element.Arguments)
            {
                if (!argument.IsRequired)
                    isOptional = true;

                if (usage.Length > 0)
                    usage.Append(' ');

                if (isOptional)
                    usage.Append('[');

                usage.Append(argument.Name);

                if (isOptional)
                    usage.Append(']');
            }
        }

        if (executingElementContext.Element.Options is not null)
        {
            foreach (var option in executingElementContext.Element.Options)
            {
                if (usage.Length > 0)
                    usage.Append(' ');

                if (!option.Value.IsRequired)
                    usage.Append('[');

                usage.Append(option.Value.Name);

                if (option.Value.AcceptsValue)
                {
                    if (option.Value.ValueLabel is not null)
                    {
                        usage.Append(" <");
                        usage.Append(option.Value.ValueLabel);
                        usage.Append('>');
                    }
                    else
                        usage.Append(" <value>");
                }

                if (!option.Value.IsRequired)
                    usage.Append(']');
            }
        }

        usage.Insert(0, new string(' ', 4));

        outputBuilder.AppendLine(usage.ToString());
    }

}
