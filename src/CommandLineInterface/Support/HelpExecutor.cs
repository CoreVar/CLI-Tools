using CoreVar.CommandLineInterface.Interfaces;
using CoreVar.CommandLineInterface.Runtime;
using CoreVar.CommandLineInterface.Utilities;
using System.Text;

namespace CoreVar.CommandLineInterface.Support;

using CoreVar.CommandLineInterface.Builders;

public class HelpExecutor(IServiceProvider serviceProvider, IApplicationContext appContext, IConsoleControl console, CommandLineOptions options) : IHelpExecutor
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
            outputBuilder.AppendLine($"{Text("Usage")}:");

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
            maxHeaderLength = Math.Max(maxHeaderLength, executingCommand.Element.Arguments.Max(a => FormatArgument(a).Length));

        if (executingCommand.Element.Options?.Count > 0)
            maxHeaderLength = Math.Max(maxHeaderLength, executingCommand.Element.Options.Where(o => !o.Value.IsHidden).Select(o =>
            {
                if (o.Value.Aliases?.Count > 0)
                    return o.Value.Name.Length + o.Value.Aliases.Sum(a => a.Length + 1);
                else
                    return o.Value.Name.Length;
            }).DefaultIfEmpty(0).Max());

        if (executingCommand.Element.Children.Count > 0)
        {
            maxHeaderLength = Math.Max(maxHeaderLength, executingCommand.Element.Children.Where(e => !e.Value.IsHidden).Select(e => e.Value.Name.Length).DefaultIfEmpty(0).Max());
        
            outputBuilder.AppendLine($"{Text("Commands")}:");
            foreach (var child in executingCommand.Element.Children.Where(child => !child.Value.IsHidden))
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
            outputBuilder.AppendLine($"{Text("Arguments")}:");

            foreach (var argument in executingCommand.Element.Arguments)
            {
                outputBuilder.Append(new string(' ', 4));
                var argumentHeader = FormatArgument(argument);
                outputBuilder.Append(argumentHeader);
                if (argument.Description is not null)
                {
                    outputBuilder.Append(new string(' ', maxHeaderLength - argumentHeader.Length + 4));
                    outputBuilder.AppendLine(argument.Description);
                }
                else
                    outputBuilder.AppendLine(string.Empty);
            }

            outputBuilder.AppendLine(string.Empty);
        }

        if (executingCommand.Element.Options?.Count > 0)
        {
            outputBuilder.AppendLine($"{Text("Options")}:");

            foreach (var option in executingCommand.Element.Options.Where(option => !option.Value.IsHidden))
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
                var details = DescribeOption(option.Value);
                if (details is not null)
                {
                    outputBuilder.Append(new string(' ', maxHeaderLength - headerLength + 4));
                    outputBuilder.AppendLine(details);
                }
                else
                    outputBuilder.AppendLine(string.Empty);
            }

            outputBuilder.AppendLine(string.Empty);
        }

        await console.Write(outputBuilder.ToString());
    }

    private string Text(string value) => options.Localize?.Invoke(value) ?? value;

    private static string FormatArgument(CommandTreeArgument argument)
        => argument.IsVariadic ? $"{argument.Name}..." : argument.Name;

    private static string? DescribeOption(CommandTreeOption option)
    {
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(option.Description)) details.Add(option.Description);
        if (option.DefaultValue is not null) details.Add($"default: {option.DefaultValue}");
        if (option.EnvironmentVariable is not null) details.Add($"env: {option.EnvironmentVariable}");
        if (option.ConfigurationKey is not null) details.Add($"config: {option.ConfigurationKey}");
        if (option.DeprecationMessage is not null) details.Add($"deprecated: {option.DeprecationMessage}");
        return details.Count == 0 ? null : string.Join("; ", details);
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
