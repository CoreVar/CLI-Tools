using CoreVar.CommandLineInterface.Runtime;
using System.Text;

namespace CoreVar.CommandLineInterface.Generation;

/// <summary>Generates shell-completion scripts from the command model.</summary>
public static class ShellCompletionGenerator
{
    public static string Generate(CommandTree tree, string shell)
    {
        var items = EnumerateItems(tree.Root).GroupBy(item => item.Value, StringComparer.Ordinal)
            .Select(group => group.First()).OrderBy(item => item.Value).ToArray();
        var words = items.Select(item => item.Value).ToArray();
        var executable = tree.Root.Name;
        var joined = string.Join(' ', words);
        return shell.ToLowerInvariant() switch
        {
            "bash" => $"complete -W \"{joined}\" {executable}{Environment.NewLine}",
            "zsh" => $"#compdef {executable}{Environment.NewLine}_arguments '*: :({joined})'{Environment.NewLine}",
            "fish" => string.Join(Environment.NewLine, words.Select(word => $"complete -c {executable} -a '{word}'")) + Environment.NewLine,
            "powershell" or "pwsh" => GeneratePowerShell(executable, items),
            _ => throw new ArgumentException($"Unsupported shell '{shell}'. Use bash, zsh, fish, or powershell.", nameof(shell))
        };
    }

    private static IEnumerable<CompletionItem> EnumerateItems(CommandTreeElement element)
    {
        foreach (var child in element.Children.Values.Distinct().Where(child => !child.IsHidden))
        {
            yield return new(child.Name, child.Description ?? child.Name);
            foreach (var alias in child.Aliases) yield return new(alias, $"Alias for {child.Name}");
            foreach (var nested in EnumerateItems(child)) yield return nested;
        }
        if (element.Options is null) yield break;
        foreach (var option in element.Options.Values.Where(option => !option.IsHidden))
        {
            yield return new(option.Name, option.Description ?? option.Name);
            if (option.Aliases is not null)
                foreach (var alias in option.Aliases) yield return new(alias, $"Alias for {option.Name}");
            foreach (var completion in option.Completions) yield return new(completion, completion);
        }
    }

    private static string GeneratePowerShell(string executable, IReadOnlyCollection<CompletionItem> items)
    {
        static string Quote(string value) => value.Replace("'", "''");
        var entries = string.Join(',', items.Select(item =>
            $"[System.Management.Automation.CompletionResult]::new('{Quote(item.Value)}','{Quote(item.Value)}','ParameterValue','{Quote(item.Description)}')"));
        return $"Register-ArgumentCompleter -Native -CommandName '{Quote(executable)}' -ScriptBlock {{ param($wordToComplete) @({entries}) | Where-Object {{ $_.CompletionText -like \"$wordToComplete*\" }} }}{Environment.NewLine}";
    }

    private sealed record CompletionItem(string Value, string Description);
}
