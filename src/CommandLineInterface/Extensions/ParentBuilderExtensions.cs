using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Support;

namespace CoreVar.CommandLineInterface;

public static class ParentBuilderExtensions
{

    public static IComponentBuilder<T> Component<T>(this IExecutableBuilder source, SourceGeneratedComponentReference<T> reference) where T : CommandLineComponent
    {
        var referenceInternals = (ISourceGeneratedComponentInternals)reference;
        referenceInternals.Build(source);

        return new ComponentBuilder<T>();
    }

    public static IComponentBuilder<T> Components<T>(this IExecutableBuilder source) where T : ComponentContext, new()
    {
        var components = new T();
        var componentsInternals = (ISourceGeneratedComponentInternals)components;
        componentsInternals.Build(source);

        return new ComponentBuilder<T>();
    }

}
