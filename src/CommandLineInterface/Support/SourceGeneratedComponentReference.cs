using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;

namespace CoreVar.CommandLineInterface.Support;

public class SourceGeneratedComponentReference<T>(Action<IExecutableBuilder> handler) : ISourceGeneratedComponentInternals
{

    void ISourceGeneratedComponentInternals.Build(IExecutableBuilder builder)
        => handler(builder);

}