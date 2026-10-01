using CoreVar.CommandLineInterface.Builders;

namespace CoreVar.CommandLineInterface.Modules;

/// <summary>A module compiled into a CLI application.</summary>
public abstract class CliModule
{
    public abstract void Build(IExecutableBuilder module);
}
