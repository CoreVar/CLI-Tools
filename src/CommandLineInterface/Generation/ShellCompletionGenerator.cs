using CoreVar.CommandLineInterface.Runtime;
using System.Text;

namespace CoreVar.CommandLineInterface.Generation;

/// <summary>Generates shell-completion scripts from the command model.</summary>
public static class ShellCompletionGenerator
{
    public static string Generate(CommandTree tree, string shell)
    {
        var words = EnumerateWords(tree.Root).Distinct(StringComparer.Ordinal).Order().ToArray();
        var executable = tree.Root.Name;
        var joined = string.Join(' ', words);
        return shell.ToLowerInvariant() switch
        {
            "bash" => $"complete -W \"{joined}\" {executable}{Environment.NewLine}",
            "zsh" => $"#compdef {executable}{Environment.NewLine}_arguments '*: :({joined})'{Environment.NewLine}",
            "fish" => string.Join(Environment.NewLine, words.Select(word => $"complete -c {executable} -a '{word}'")) + Environment.NewLine,
            "powershell" or "pwsh" => GeneratePowerShell(executable, words),
            _ => throw new ArgumentException($"Unsupported shell '{shell}'. Use bash, zsh, fish, or powershell.", nameof(shell))
        };
    }

    private static IEnumerable<string> EnumerateWords(CommandTreeElement element)
    {
        foreach (var child in element.Children.Values.Distinct().Where(child => !child.IsHidden))
        {
            yield return child.Name;
            foreach (var alias in child.Aliases) yield return alias;
            foreach (var nested in EnumerateWords(child)) yield return nested;
        }
        if (element.Options is null) yield break;
        foreach (var option in element.Options.Values.Where(option => !option.IsHidden))
        {
            yield return option.Name;
            if (option.Aliases is not null)
                foreach (var alias in option.Aliases) yield return alias;
            foreach (var completion in option.Completions) yield return completion;
        }
    }

    private static string GeneratePowerShell(string executable, IReadOnlyCollection<string> words)
    {
        var quoted = string.Join(',', words.Select(word => $"'{word.Replace("'", "''")}'"));
        return $"Register-ArgumentCompleter -Native -CommandName '{executable}' -ScriptBlock {{ param($wordToComplete) @({quoted}) | Where-Object {{ $_ -like \"$wordToComplete*\" }} }}{Environment.NewLine}";
    }
}
