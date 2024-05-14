using CoreVar.CommandLineInterface.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace CoreVar.CommandLineInterface.SourceGenerator;

partial class ComponentSourceGenerator
{

    private class Parser(KnownTypeSymbols knownSymbols)
    {
        public const string ComponentAttributeFullName = "CoreVar.CommandLineInterface.ComponentAttribute`1";
        private Location? _contextClassLocation;
        private List<ComponentSpec> _generatedComponents = [];

        public ComponentContextSpec? ParseComponentContext(ClassDeclarationSyntax contextClass, SemanticModel semanticModel, CancellationToken cancellationToken)
        {

#if DEBUG
            //if (!Debugger.IsAttached)
            //    Debugger.Launch();
#endif

            INamedTypeSymbol? contextTypeSymbol = (INamedTypeSymbol?)semanticModel.GetDeclaredSymbol(contextClass);

            Debug.Assert(contextTypeSymbol is not null);

            _contextClassLocation = contextTypeSymbol!.Locations.FirstOrDefault();

            Debug.Assert(_contextClassLocation is not null);

            if (!knownSymbols.ComponentContextType.IsAssignableFrom(contextTypeSymbol))
            {
                return null;
            }

            ParseComponentContextAttributes(contextTypeSymbol, out var rootComponentTypes);

            if (rootComponentTypes is null)
            {

                return null;
            }

            foreach (var componentTypeToGenerate in rootComponentTypes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var commandDefinition = ParseCommandSpec(componentTypeToGenerate);
                _generatedComponents.Add(commandDefinition);
            }

            var result = new ComponentContextSpec
            {
                Type = contextTypeSymbol,
                Components = [.. _generatedComponents]
            };

            return result;
        }

        private void ParseComponentContextAttributes(INamedTypeSymbol typeSymbol, out List<ITypeSymbol>? rootComponentTypes)
        {
            rootComponentTypes = null;
            foreach (var attributeData in typeSymbol.GetAttributes())
            {
                if (attributeData.AttributeClass is not null &&
                    attributeData.AttributeClass.IsGenericType &&
                    SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass.OriginalDefinition, knownSymbols.ComponentAttributeType))
                {
                    var componentType = attributeData.AttributeClass.TypeArguments[0];
                    rootComponentTypes ??= [];
                    rootComponentTypes.Add(componentType);
                }
            }
        }

        private ComponentSpec ParseCommandSpec(ITypeSymbol componentTypeToGenerate)
        {

            string? commandName = null;
            string? commandDescription = null;
            List<ComponentSpec> nestedCommands = [];

            foreach (var attribute in componentTypeToGenerate.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownSymbols.CommandNameAttributeType))
                    commandName = (string)attribute.ConstructorArguments[0].Value!;
                else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownSymbols.DescriptionAttributeType))
                    commandDescription = (string)attribute.ConstructorArguments[0].Value!;
                else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass!.OriginalDefinition, knownSymbols.ComponentAttributeType))
                {
                    var nestedComponentType = attribute.AttributeClass.TypeArguments[0];
                    nestedCommands.Add(ParseCommandSpec(nestedComponentType));
                }
            }

            if (commandName is null)
                return default!; // Should return null

            var componentSpec = new ComponentSpec
            {
                Name = commandName,
                Description = commandDescription,
                Type = componentTypeToGenerate
            };

            foreach (var nestedCommand in nestedCommands)
                componentSpec.NestedCommands.Add(nestedCommand);

            foreach (var commandMember in componentTypeToGenerate.GetMembers())
            {
                if (commandMember is IPropertySymbol commandProperty)
                {
                    CommandOptionSpec? commandOption = default;
                    CommandArgumentSpec? commandArgument = default;
                    string? commandElementDescription = default;
                    bool? isRequired = default;
                    foreach (var attribute in commandProperty.GetAttributes())
                    {
                        if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownSymbols.CommandOptionAttributeType))
                        {
                            var optionName = (string)attribute.ConstructorArguments[0].Value!;
                            commandOption = new CommandOptionSpec
                            {
                                Name = optionName,
                                TargetPropertyName = commandProperty.Name,
                                TargetPropertyType = commandProperty.Type
                            };
                        }
                        else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownSymbols.CommandArgumentAttributeType))
                        {
                            var attributeName = (string)attribute.ConstructorArguments[0].Value!;
                            int index = -1;
                            if (attribute.NamedArguments.Length > 0)
                            {
                                foreach (var namedArgument in attribute.NamedArguments)
                                    if (namedArgument.Key == "Index")
                                        index = (int)attribute.NamedArguments[0].Value.Value!;
                            }

                            if (attributeName is not null)
                            {
                                commandArgument = new CommandArgumentSpec
                                {
                                    Name = attributeName,
                                    Index = index,
                                    TargetPropertyName = commandProperty.Name,
                                    TargetPropertyType = commandProperty.Type
                                };
                            }
                        }
                        else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownSymbols.DescriptionAttributeType))
                        {
                            commandElementDescription = (string)attribute.ConstructorArguments[0].Value!;
                        }
                        else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownSymbols.RequiredAttributeType))
                        {
                            isRequired = true;
                        }
                        else if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, knownSymbols.OptionalAttributeType))
                        {
                            isRequired = false;
                        }
                    }

                    if (commandArgument is not null)
                    {
                        commandArgument.Description = commandElementDescription;
                        commandArgument.IsRequired = isRequired ?? true; // TODO: Handle fallback value for nullable types

                        componentSpec.Arguments.Add(commandArgument);
                    }
                    else if (commandOption is not null)
                    {
                        commandOption.Description = commandElementDescription;
                        commandOption.IsRequired = isRequired ?? true; // TODO: Handle fallback value for nullable types

                        componentSpec.Options.Add(commandOption);
                    }
                }
                else if (commandMember is IMethodSymbol commandMethod)
                {
                    if (commandMethod.Name == "Execute" ||
                        commandMethod.Name == "ExecuteAsync")
                    {
                        if (!commandMethod.IsStatic && commandMethod.DeclaredAccessibility == Accessibility.Public)
                            componentSpec.ExecuteMethodName = commandMethod.Name;
                    }
                    else if (commandMethod.Name == "SetupHostBuilder")
                    {
                        if (commandMethod.IsStatic && commandMethod.DeclaredAccessibility == Accessibility.Public)
                            componentSpec.SetupHostBuilderMethodName = commandMethod.Name;
                    }
                    else if (commandMethod.Name == "SetupHost")
                    {
                        if (commandMethod.IsStatic && commandMethod.DeclaredAccessibility == Accessibility.Public)
                            componentSpec.SetupHostMethodName = commandMethod.Name;
                    }
                    else if (commandMethod.Name == "ConfigureServices" ||
                        commandMethod.Name == "SetupServices")
                    {
                        if (commandMethod.IsStatic && commandMethod.DeclaredAccessibility == Accessibility.Public)
                            componentSpec.SetupServicesMethodName = commandMethod.Name;
                    }
                }
            }
            return componentSpec;
        }

    }
}
