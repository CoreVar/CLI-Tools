using CoreVar.CommandLineInterface.Runtime;
using System.Text;

namespace CoreVar.CommandLineInterface.Generation;

/// <summary>Generates Markdown command reference documentation from the runtime model.</summary>
public static class CommandDocumentationGenerator
{
    public static string GenerateMarkdown(CommandTree tree)
    {
        var output = new StringBuilder();
        WriteElement(output, tree.Root, tree.Root.Name);
        return output.ToString();
    }

    private static void WriteElement(StringBuilder output, CommandTreeElement element, string path)
    {
        if (element.IsHidden) return;
        output.Append("## `").Append(path).AppendLine("`");
        if (element.Description is not null) output.AppendLine().AppendLine(element.Description);
        if (element.DeprecationMessage is not null) output.AppendLine().Append("> Deprecated: ").AppendLine(element.DeprecationMessage);

        if (element.Arguments?.Count > 0)
        {
            output.AppendLine().AppendLine("### Arguments").AppendLine();
            foreach (var argument in element.Arguments)
                output.Append("- `").Append(argument.Name).Append("` — ").AppendLine(argument.Description ?? (argument.IsRequired ? "Required." : "Optional."));
        }

        if (element.Options?.Values.Any(option => !option.IsHidden) == true)
        {
            output.AppendLine().AppendLine("### Options").AppendLine();
            foreach (var option in element.Options.Values.Where(option => !option.IsHidden))
            {
                output.Append("- `").Append(option.Name).Append('`');
                if (option.Aliases?.Count > 0) output.Append(" (`").Append(string.Join("`, `", option.Aliases)).Append("`)");
                output.Append(" — ").Append(option.Description ?? (option.IsRequired ? "Required." : "Optional."));
                if (option.DefaultValue is not null) output.Append(" Default: `").Append(option.DefaultValue).Append("`.");
                output.AppendLine();
            }
        }
        output.AppendLine();

        foreach (var child in element.Children.Values.Distinct())
            WriteElement(output, child, $"{path} {child.Name}");
    }
}
