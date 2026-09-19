using CoreVar.CommandLineInterface.Builders;

namespace CoreVar.CommandLineInterface.Modules;

public static class ModuleBuilderExtensions
{
    /// <summary>Adds a module compiled into the host CLI.</summary>
    public static ICommandLineBuilder Module<TModule>(this ICommandLineBuilder builder)
        where TModule : CliModule, new()
    {
        new TModule().Build(builder);
        return builder;
    }

    /// <summary>Adds a module instance compiled into the host CLI.</summary>
    public static ICommandLineBuilder Module(this ICommandLineBuilder builder, CliModule module)
    {
        module.Build(builder);
        return builder;
    }
}
