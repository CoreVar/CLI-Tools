using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Execution;
using CoreVar.CommandLineInterface.Validation;

namespace CoreVar.CommandLineInterface;

/// <summary>Configures cross-cutting command behavior without changing handler code.</summary>
public static class AdvancedBuilderExtensions
{
    public static T Use<T>(this T builder, CommandMiddleware middleware) where T : IExecutableBuilder
    {
        ArgumentNullException.ThrowIfNull(middleware);
        ((IExecutableBuilderInternals)builder).Middleware.Add(middleware);
        return builder;
    }

    public static T Validate<T>(this T builder, Func<CommandExecutionContext, ValidationResult> validator) where T : IExecutableBuilder
        => builder.Validate(context => ValueTask.FromResult(validator(context)));

    public static T Validate<T>(this T builder, Func<CommandExecutionContext, ValueTask<ValidationResult>> validator) where T : IExecutableBuilder
    {
        ArgumentNullException.ThrowIfNull(validator);
        ((IExecutableBuilderInternals)builder).Validators.Add(validator);
        return builder;
    }

    public static T Alias<T>(this T builder, params string[] aliases) where T : IExecutableBuilder
    {
        var target = ((IExecutableBuilderInternals)builder).Aliases;
        foreach (var alias in aliases.Where(a => !string.IsNullOrWhiteSpace(a)))
            target.Add(alias);
        return builder;
    }

    public static T Hidden<T>(this T builder, bool hidden = true) where T : IExecutableBuilder
    {
        ((IExecutableBuilderInternals)builder).IsHidden = hidden;
        return builder;
    }

    public static T Deprecated<T>(this T builder, string message) where T : IExecutableBuilder
    {
        ((IExecutableBuilderInternals)builder).DeprecationMessage = message;
        return builder;
    }

    public static ICommandLineBuilder Version(this ICommandLineBuilder builder, string version)
    {
        ((ICommandLineBuilderInternals)builder).CommandLineOptions.Version = version;
        return builder;
    }

    public static ICommandLineBuilder Suggestions(this ICommandLineBuilder builder, bool enabled = true)
    {
        ((ICommandLineBuilderInternals)builder).CommandLineOptions.EnableSuggestions = enabled;
        return builder;
    }

    /// <summary>Localizes built-in labels while leaving command metadata under application control.</summary>
    public static ICommandLineBuilder Localize(this ICommandLineBuilder builder, Func<string, string> translator)
    {
        ArgumentNullException.ThrowIfNull(translator);
        ((ICommandLineBuilderInternals)builder).CommandLineOptions.Localize = translator;
        return builder;
    }

    /// <summary>Sets the prefix used by parameterless environment binding.</summary>
    public static ICommandLineBuilder EnvironmentPrefix(this ICommandLineBuilder builder, string prefix)
    {
        ((ICommandLineBuilderInternals)builder).CommandLineOptions.EnvironmentPrefix = prefix;
        return builder;
    }
}
