using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using Microsoft.Extensions.DependencyInjection;

namespace CoreVar.CommandLineInterface;

public static class CommandPromptExtensions
{
    public static ICommandOptionBuilder<T> PromptIfMissing<T>(this ICommandOptionBuilder<T> builder, string? label = null, bool secret = false)
    {
        var metadata = (ICommandOptionBuilderInternals)builder;
        metadata.PromptIfMissing = true;
        metadata.PromptLabel = label;
        metadata.Secret |= secret;
        return builder;
    }

    public static ICommandArgumentBuilder<T> PromptIfMissing<T>(this ICommandArgumentBuilder<T> builder, string? label = null, bool secret = false)
    {
        var metadata = (ICommandArgumentBuilderInternals)builder;
        metadata.PromptIfMissing = true;
        metadata.PromptLabel = label;
        metadata.Secret |= secret;
        return builder;
    }

    public static ICommandOptionBuilder<T> Secret<T>(this ICommandOptionBuilder<T> builder)
    {
        ((ICommandOptionBuilderInternals)builder).Secret = true;
        return builder;
    }

    public static ICommandArgumentBuilder<T> Secret<T>(this ICommandArgumentBuilder<T> builder)
    {
        ((ICommandArgumentBuilderInternals)builder).Secret = true;
        return builder;
    }

    /// <summary>Requests a value after the caller has resolved explicit input sources.</summary>
    public static async ValueTask<string> PromptAsync(this CommandExecutionContext context, CommandPromptRequest request, bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(request);
        context.HasSecretInput |= request.IsSecret;
        context.CancellationToken.ThrowIfCancellationRequested();
        if (!enabled || !context.EnablePrompts || !context.Services.GetRequiredService<CommandLineOptions>().EnablePrompts)
            throw new CommandPromptException();
        try
        {
            var prompt = context.Services.GetService<ICommandPromptService>();
            if (prompt is null || !prompt.IsInteractive) throw new CommandPromptException();
            var value = await prompt.ReadAsync(request, context.CancellationToken);
            context.CancellationToken.ThrowIfCancellationRequested();
            if (value is null || (value.Length == 0 && !request.AllowEmpty)) throw new CommandPromptException();
            return value;
        }
        catch (OperationCanceledException) { throw; }
        catch { throw new CommandPromptException(); }
    }
}
