using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;

namespace CoreVar.CommandLineInterface;

public abstract class ComponentContext : ISourceGeneratedComponentInternals
{
    void ISourceGeneratedComponentInternals.Build(IExecutableBuilder builder)
        => Build(builder);

    protected virtual void Build(IExecutableBuilder builder)
    {

    }

}
