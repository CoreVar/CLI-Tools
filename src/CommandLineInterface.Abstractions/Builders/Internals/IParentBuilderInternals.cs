using CoreVar.CommandLineInterface.Builders;

namespace CoreVar.CommandLineInterface.Builders.Internals;

public interface IParentBuilderInternals : IBuilderInternals
{

    Dictionary<string, Action<ICommandBuilder>> Children { get; }

}