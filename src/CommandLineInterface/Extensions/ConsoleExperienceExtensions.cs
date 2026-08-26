using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CoreVar.CommandLineInterface;

/// <summary>Small, dependency-free helpers for interactive and machine-readable commands.</summary>
public static class ConsoleExperienceExtensions
{
    /// <summary>Prompts for a value and optionally validates it before returning.</summary>
    public static async ValueTask<string> PromptAsync(this CommandExecutionContext context, string prompt,
        Func<string, bool>? validator = null, string? validationMessage = null)
    {
        while (true)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            await context.Console.Write(prompt).ConfigureAwait(false);
            var value = await context.Console.ReadLine().ConfigureAwait(false);
            if (validator is null || validator(value)) return value;
            await context.Console.WriteErrorLine(validationMessage ?? "Invalid value.").ConfigureAwait(false);
        }
    }

    /// <summary>Prompts for a yes/no decision.</summary>
    public static async ValueTask<bool> ConfirmAsync(this CommandExecutionContext context, string prompt, bool defaultValue = false)
    {
        var suffix = defaultValue ? " [Y/n] " : " [y/N] ";
        var value = (await context.PromptAsync(prompt + suffix).ConfigureAwait(false)).Trim();
        if (value.Length == 0) return defaultValue;
        return value.Equals("y", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Writes an object as JSON.</summary>
    public static ValueTask WriteJsonAsync<T>(this CommandExecutionContext context, T value, JsonTypeInfo<T> typeInfo)
        => context.Console.WriteLine(JsonSerializer.Serialize(value, typeInfo));

    /// <summary>Writes records as newline-delimited JSON for pipelines.</summary>
    public static async ValueTask WriteJsonLinesAsync<T>(this CommandExecutionContext context, IEnumerable<T> values,
        JsonTypeInfo<T> typeInfo)
    {
        foreach (var value in values)
            await context.Console.WriteLine(JsonSerializer.Serialize(value, typeInfo)).ConfigureAwait(false);
    }
}
