using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Builders.Internals;
using CoreVar.CommandLineInterface.Support;

namespace CoreVar.CommandLineInterface;

public static class ParentBuilderExtensions
{

    /// <summary>
    /// Defines a component for the command line application.
    /// </summary>
    /// <typeparam name="T">The type of the component.</typeparam>
    /// <param name="source">The builder.</param>
    /// <param name="reference">The reference to the component.</param>
    /// <returns>The component builder.</returns>
    public static IComponentBuilder<T> Component<T>(this IExecutableBuilder source, SourceGeneratedComponentReference<T> reference) where T : CommandLineComponent
    {
        var referenceInternals = (ISourceGeneratedComponentInternals)reference;
        referenceInternals.Build(source);

        return new ComponentBuilder<T>();
    }

    /// <summary>
    /// Defines components for the command line application.
    /// </summary>
    /// <typeparam name="T">The type of the component context.</typeparam>
    /// <param name="source">The builder.</param>
    /// <returns>The component builder.</returns>
    public static IComponentBuilder<T> Components<T>(this IExecutableBuilder source) where T : ComponentContext, new()
    {
        var components = new T();
        var componentsInternals = (ISourceGeneratedComponentInternals)components;
        componentsInternals.Build(source);

        return new ComponentBuilder<T>();
    }

}
