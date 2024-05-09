using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CoreVar.CommandLineInterface.SourceGenerator;

public class ComponentClassSyntaxReceiver : ISyntaxContextReceiver
{

    public Dictionary<string, CommandDefinition> CommandDefinitions { get; } = [];

    public List<ComponentContextDefinition> ComponentContexts { get; } = [];

    public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
    {

    }

    public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
    {
        if (context.Node is ClassDeclarationSyntax cds)
        {
            if (cds.BaseList is null)
                return;

            if (cds.BaseList.Types.Any(t =>
                context.SemanticModel.GetTypeInfo(t.Type).Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::CoreVar.CommandLineInterface.ComponentContext"))
            {
                var components = new HashSet<string>();
                foreach (var attributeList in cds.AttributeLists)
                    foreach (var attribute in attributeList.Attributes)
                    {
                        var attributeType = context.SemanticModel.GetTypeInfo(attribute);
                        if (attributeType.Type is not null
                            && attributeType.Type.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::CoreVar.CommandLineInterface")
                        {
                            switch (attributeType.Type.Name)
                            {
                                case "ComponentAttribute":
                                    if (attribute.Name is not GenericNameSyntax genericNameSyntax || genericNameSyntax.TypeArgumentList.Arguments.Count != 1)
                                        throw new InvalidOperationException("The type list cannot be null and must contain one type for ComponentAttribute<>.");

                                    var typeArgument = genericNameSyntax.TypeArgumentList.Arguments[0];
                                    var typeInfo = context.SemanticModel.GetTypeInfo(typeArgument);

                                    components.Add(typeInfo.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));

                                    break;
                            }
                        }
                    }
                ComponentContexts.Add(new ComponentContextDefinition
                {
                    ClassDeclaration = cds,
                    Components = components
                });
            }
            else if (cds.BaseList.Types.Any(t => 
                context.SemanticModel.GetTypeInfo(t.Type).Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::CoreVar.CommandLineInterface.CommandLineComponent"))
            {
                string? commandName = null;
                string? commandDescription = null;
                List<string> nestedCommands = [];

                foreach (var attributeList in cds.AttributeLists)
                {
                    foreach (var attribute in attributeList.Attributes)
                    {

                        var attributeType = context.SemanticModel.GetTypeInfo(attribute);
                        if (attributeType.Type is not null
                            && attributeType.Type.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) == "global::CoreVar.CommandLineInterface")
                        {
                            switch (attributeType.Type.Name)
                            {
                                case "CommandNameAttribute":
                                    var commandNameValue = context.SemanticModel.GetConstantValue(attribute.ArgumentList!.Arguments[0].Expression);
                                    commandName = commandNameValue.HasValue ? (string)commandNameValue.Value! : null;
                                    break;
                                case "DescriptionAttribute":
                                    var descriptionValue = context.SemanticModel.GetConstantValue(attribute.ArgumentList!.Arguments[0].Expression);
                                    commandDescription = descriptionValue.HasValue ? (string)descriptionValue.Value! : null;
                                    break;
                                case "ComponentAttribute":

                                    if (attribute.Name is not GenericNameSyntax genericNameSyntax || genericNameSyntax.TypeArgumentList.Arguments.Count != 1)
                                        throw new InvalidOperationException("The type list cannot be null and must contain one type for ComponentAttribute<>.");

                                    var typeArgument = genericNameSyntax.TypeArgumentList.Arguments[0];
                                    var typeInfo = context.SemanticModel.GetTypeInfo(typeArgument);

                                    nestedCommands.Add(typeInfo.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                                    break;
                            }
                        }
                    }
                }

                if (commandName is null)
                    return;

                var typeSymbol = context.SemanticModel.GetDeclaredSymbol(cds)!;
                var commandDefinition = new CommandDefinition
                {
                    Name = commandName,
                    Description = commandDescription,
                    Type = typeSymbol
                };

                foreach (var nestedCommandTypeInfo in nestedCommands)
                    commandDefinition.NestedCommands.Add(nestedCommandTypeInfo);

#if DEBUG
                //if (!Debugger.IsAttached)
                //    Debugger.Launch();
#endif

                foreach (var commandMember in cds.Members)
                {
                    if (commandMember is PropertyDeclarationSyntax commandProperty)
                    {
                        CommandOptionDefinition? commandOption = default;
                        CommandArgumentDefinition? commandArgument = default;
                        string? commandElementDescription = default;
                        bool? isRequired = default;
                        foreach (var attributeList in commandProperty.AttributeLists)
                            foreach (var attribute in attributeList.Attributes)
                            {
                                var attributeType = context.SemanticModel.GetTypeInfo(attribute);
                                var attributeTypeName = attributeType.Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                                if (attributeTypeName == "global::CoreVar.CommandLineInterface.CommandOptionAttribute")
                                {
                                    var optionNameValue = context.SemanticModel.GetConstantValue(attribute.ArgumentList!.Arguments[0].Expression);
                                    var optionName = optionNameValue.HasValue ? (string)optionNameValue.Value! : null;

                                    if (optionName is not null)
                                        commandOption = new CommandOptionDefinition
                                        {
                                            Name = optionName,
                                            TargetProperty = commandProperty
                                        };
                                }
                                else if (attributeTypeName == "global::CoreVar.CommandLineInterface.CommandArgumentAttribute")
                                {
                                    var attributeNameValue = context.SemanticModel.GetConstantValue(attribute.ArgumentList!.Arguments[0].Expression);
                                    var attributeName = attributeNameValue.HasValue ? (string)attributeNameValue.Value! : null;
                                    int index = -1;
                                    if (attribute.ArgumentList.Arguments.Count > 1)
                                    {
                                        var indexArgument = attribute.ArgumentList.Arguments[1];

                                        if (indexArgument.NameEquals is not null && indexArgument.NameEquals.Name.ToFullString() == "Index")
                                        {
                                            var indexValue = context.SemanticModel.GetConstantValue(indexArgument.Expression);
                                            if (indexValue.HasValue)
                                                index = (int)indexValue.Value!;
                                        }
                                    }

                                    if (attributeName is not null)
                                    {
                                        commandArgument = new CommandArgumentDefinition
                                        {
                                            Name = attributeName,
                                            Index = index,
                                            TargetProperty = commandProperty
                                        };
                                    }
                                }
                                else if (attributeTypeName == "global::CoreVar.CommandLineInterface.DescriptionAttribute")
                                {
                                    var descriptionValue = context.SemanticModel.GetConstantValue(attribute.ArgumentList!.Arguments[0].Expression);
                                    commandElementDescription = descriptionValue.HasValue ? (string)descriptionValue.Value! : null;
                                }
                                else if (attributeTypeName == "global::CoreVar.CommandLineInterface.RequiredAttribute")
                                {
                                    isRequired = true;
                                }
                                else if (attributeTypeName == "global::CoreVar.CommandLineInterface.OptionalAttribute")
                                {
                                    isRequired = false;
                                }
                            }

                        if (commandArgument is not null)
                        {
                            commandArgument.Description = commandElementDescription;
                            commandArgument.IsRequired = isRequired ?? true; // TODO: Handle fallback value for nullable types

                            commandDefinition.Arguments.Add(commandArgument);
                        }
                        else if (commandOption is not null)
                        {
                            commandOption.Description = commandElementDescription;
                            commandOption.IsRequired = isRequired ?? true; // TODO: Handle fallback value for nullable types

                            commandDefinition.Options.Add(commandOption);
                        }
                    }
                    else if (commandMember is MethodDeclarationSyntax commandMethod)
                    {
                        if (commandMethod.Identifier.ValueText == "Execute" ||
                            commandMethod.Identifier.ValueText == "ExecuteAsync")
                        {
                            var hasStaticModifier = false;
                            var hasPublicModifier = false;
                            foreach (var modifier in commandMethod.Modifiers)
                            {
                                if (modifier.ValueText == "static")
                                    hasStaticModifier = true;
                                else if (modifier.ValueText == "public")
                                    hasPublicModifier = true;
                            }

                            if (!hasStaticModifier && hasPublicModifier)
                            {
                                commandDefinition.ExecuteMethod = commandMethod;
                            }
                        }
                        else if (commandMethod.Identifier.ValueText == "SetupHostBuilder")
                        {
                            var hasStaticModifier = false;
                            var hasPublicModifier = false;
                            foreach (var modifier in commandMethod.Modifiers)
                            {
                                if (modifier.ValueText == "static")
                                    hasStaticModifier = true;
                                else if (modifier.ValueText == "public")
                                    hasPublicModifier = true;
                            }

                            if (hasStaticModifier && hasPublicModifier)
                            {
                                commandDefinition.SetupHostBuilderMethod = commandMethod;
                            }
                        }
                        else if (commandMethod.Identifier.ValueText == "SetupHost")
                        {
                            var hasStaticModifier = false;
                            var hasPublicModifier = false;
                            foreach (var modifier in commandMethod.Modifiers)
                            {
                                if (modifier.ValueText == "static")
                                    hasStaticModifier = true;
                                else if (modifier.ValueText == "public")
                                    hasPublicModifier = true;
                            }

                            if (hasStaticModifier && hasPublicModifier)
                            {
                                commandDefinition.SetupHostMethod = commandMethod;
                            }
                        }
                        else if (commandMethod.Identifier.ValueText == "ConfigureServices" ||
                            commandMethod.Identifier.ValueText == "SetupServices")
                        {
                            var hasStaticModifier = false;
                            var hasPublicModifier = false;
                            foreach (var modifier in commandMethod.Modifiers)
                            {
                                if (modifier.ValueText == "static")
                                    hasStaticModifier = true;
                                else if (modifier.ValueText == "public")
                                    hasPublicModifier = true;
                            }

                            if (hasStaticModifier && hasPublicModifier)
                            {
                                commandDefinition.SetupServicesMethod = commandMethod;
                            }
                        }
                    }
                }

                CommandDefinitions.Add(typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), commandDefinition);
            }
        }
    }
}
