﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CoreVar.CommandLineInterface.SourceGenerator;

[Generator]
public class ComponentSourceGenerator : ISourceGenerator
{
    private ComponentClassSyntaxReceiver _componentClassSyntaxReceiver = new();

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => _componentClassSyntaxReceiver);
    }

    public void Execute(GeneratorExecutionContext context)
    {
#if DEBUG
        //if (!Debugger.IsAttached)
        //    Debugger.Launch();
#endif
        foreach (var componentContext in _componentClassSyntaxReceiver.ComponentContexts)
        {
            var sourceBuilder = new StringBuilder();
            var indent = 0;
            sourceBuilder.Append($@"// <auto-generated/>
using CoreVar.CommandLineInterface;
using CoreVar.CommandLineInterface.Builders;
using CoreVar.CommandLineInterface.Support;
using CoreVar.CommandLineInterface.Builders.Internals;
using Microsoft.Extensions.DependencyInjection;
");
            var namespaceDeclaration = componentContext.ClassDeclaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
            if (namespaceDeclaration is null)
                continue;

            var typeNamespace = namespaceDeclaration.Name.ToString();
            var typeName = componentContext.ClassDeclaration.Identifier.ValueText;

            sourceBuilder.Append($@"namespace {typeNamespace}
{{");
            indent++;

            sourceBuilder.Append($@"
{new string(' ', indent * 4)}partial class {typeName}
{new string(' ', indent * 4)}{{");
            indent++;

            sourceBuilder.Append($@"
{new string(' ', indent * 4)}public static {typeName} Default {{ get; }} = new();

{new string(' ', indent * 4)}protected override void Build(IExecutableBuilder builder)
{new string(' ', indent * 4)}{{
");
            indent++;

            foreach (var componentIdentifier in componentContext.Components)
            {
                var commandDefinition = _componentClassSyntaxReceiver.CommandDefinitions[componentIdentifier];
                var componentPropertyName = commandDefinition.Type.Name;
                if (componentPropertyName.EndsWith("Component"))
                    componentPropertyName = componentPropertyName.Substring(0, componentPropertyName.Length - 9);
                
                sourceBuilder.AppendLine($@"{new string(' ', indent * 4)}((ISourceGeneratedComponentInternals){componentPropertyName}).Build(builder);");
            }

            // End of function
            indent--;
            sourceBuilder.AppendLine($@"{new string(' ', indent * 4)}}}");

            // Properties
            foreach (var componentIdentifier in componentContext.Components)
            {
                var commandDefinition = _componentClassSyntaxReceiver.CommandDefinitions[componentIdentifier];
                GenerateComponent(context, sourceBuilder, "builder", componentIdentifier, indent);
            }

            // End of class
            indent--;
            sourceBuilder.AppendLine($@"{new string(' ', indent * 4)}}}");

            // End of namespace
            indent--;
            sourceBuilder.AppendLine($@"{new string(' ', indent * 4)}}}");

            var source = sourceBuilder.ToString();
            context.AddSource($"{typeName}.g.cs", source);
        }
    }

    private void GenerateComponent(GeneratorExecutionContext context, StringBuilder sourceBuilder, string builderVariableName, string command, int indent)
    {
        var commandDefinition = _componentClassSyntaxReceiver.CommandDefinitions[command];
        var staticPropertyName = commandDefinition.Type.Name;
        if (staticPropertyName.EndsWith("Component"))
            staticPropertyName = staticPropertyName.Substring(0, staticPropertyName.Length - 9);

        sourceBuilder.AppendLine($@"{new string(' ', indent * 4)}public SourceGeneratedComponentReference<{commandDefinition.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}> {staticPropertyName} {{ get; }} = new SourceGeneratedComponentReference<{commandDefinition.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>(builder =>
{new string(' ', indent * 4)}{{");
        indent++;

        GenerateComponentBuilder(context, sourceBuilder, builderVariableName, command, indent);

        indent--;
        sourceBuilder.Append(@$"{new string(' ', indent * 4)}}});
");
    }

    private void GenerateComponentBuilder(GeneratorExecutionContext context, StringBuilder sourceBuilder, string builderVariableName, string command, int indent)
    {
        var commandDefinition = _componentClassSyntaxReceiver.CommandDefinitions[command];
        var commandParameterName = $"{CompilerSafeVariableName(commandDefinition.Name)}Command";
        var generatedCommandName = commandDefinition.Name.Replace("\"", "\\\"");
        
        sourceBuilder.AppendLine($@"{new string(' ', indent * 4)}{builderVariableName}.Command(""{generatedCommandName}"", {commandParameterName} =>
{new string(' ', indent * 4)}{{");

        indent++;

        var hasSetup =
            commandDefinition.SetupHostBuilderMethod is not null ||
            commandDefinition.SetupServicesMethod is not null ||
            commandDefinition.SetupHostMethod is not null;
        if (hasSetup)
        {
            sourceBuilder.Append($@"{new string(' ', indent * 4)}{commandParameterName}");
            indent++;
        }

        if (commandDefinition.SetupHostBuilderMethod is not null)
        {
            sourceBuilder.AppendLine();
            sourceBuilder.Append($@"{new string(' ', indent * 4)}.SetupHostBuilder({commandDefinition.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{commandDefinition.SetupHostBuilderMethod.Identifier.ValueText})");
        }

        if (commandDefinition.SetupServicesMethod is not null)
        {
            sourceBuilder.AppendLine();
            sourceBuilder.Append($@"{new string(' ', indent * 4)}.SetupHostBuilder(builder => {commandDefinition.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{commandDefinition.SetupServicesMethod.Identifier.ValueText}(builder.Services))");
        }

        if (commandDefinition.SetupHostMethod is not null)
        {
            sourceBuilder.AppendLine();
            sourceBuilder.Append($@"{new string(' ', indent * 4)}.SetupHost({commandDefinition.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}.{commandDefinition.SetupHostMethod.Identifier.ValueText})");
        }

        if (hasSetup)
        {
            sourceBuilder.AppendLine(";");
            indent--;
        }


        var propertyAssignmentsBuilder = new StringBuilder();

        foreach (var argument in commandDefinition.Arguments)
        {
            var argumentParameterName = $"{CompilerSafeVariableName(argument.Name)}Argument";

            var semanticModel = context.Compilation.GetSemanticModel(argument.TargetProperty.SyntaxTree);
            var argumentType = semanticModel.GetTypeInfo(argument.TargetProperty.Type).Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            sourceBuilder.Append($@"{new string(' ', indent * 4)}var {argumentParameterName} = {commandParameterName}.Argument<{argumentType}>({CompilerSafeString(argument.Name)})");
            indent++;

            if (argument.Description is not null)
            {
                sourceBuilder.AppendLine();
                sourceBuilder.Append($@"{new string(' ', indent * 4)}.Description({CompilerSafeString(argument.Description)})");
            }

            indent--;
            sourceBuilder.AppendLine(";");


            indent += 2;
            propertyAssignmentsBuilder.AppendLine($@"{new string(' ', indent * 4)}component.{argument.TargetProperty.Identifier.ValueText} = context.GetArgument({argumentParameterName});");
            indent -= 2;
        }

        foreach (var option in commandDefinition.Options)
        {
            var optionParameterName = $"{CompilerSafeVariableName(option.Name)}Option";

            var semanticModel = context.Compilation.GetSemanticModel(option.TargetProperty.SyntaxTree);
            var optionType = semanticModel.GetTypeInfo(option.TargetProperty.Type).Type!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            if (optionType == "bool")
                sourceBuilder.Append($@"{new string(' ', indent * 4)}var {optionParameterName} = {commandParameterName}.Option({CompilerSafeString(option.Name)})");
            else
                sourceBuilder.Append($@"{new string(' ', indent * 4)}var {optionParameterName} = {commandParameterName}.Option<{optionType}>({CompilerSafeString(option.Name)})");
            indent++;

            if (option.Description is not null)
            {
                sourceBuilder.AppendLine();
                sourceBuilder.Append($@"{new string(' ', indent * 4)}.Description({CompilerSafeString(option.Description)})");
            }

            indent--;
            sourceBuilder.AppendLine(";");


            indent += 2;
            propertyAssignmentsBuilder.AppendLine($@"{new string(' ', indent * 4)}component.{option.TargetProperty.Identifier.ValueText} = context.GetOption({optionParameterName});");
            indent -= 2;
        }

        var hasElement = commandDefinition.Description is not null ||
            commandDefinition.ExecuteMethod is not null; ;

        if (hasElement)
            sourceBuilder.Append($@"{new string(' ', indent * 4)}{commandParameterName}");

        indent++;

        if (commandDefinition.Description is not null)
        {
            sourceBuilder.AppendLine();
            sourceBuilder.Append($@"{new string(' ', indent * 4)}.Description({CompilerSafeString(commandDefinition.Description)})");
        }

        // On Execute
        if (commandDefinition.ExecuteMethod is not null)
        {
            sourceBuilder.AppendLine();
            sourceBuilder.AppendLine($@"{new string(' ', indent * 4)}.OnExecute(async context =>
{new string(' ', indent * 4)}{{");

            indent++;
            sourceBuilder.AppendLine($@"{new string(' ', indent * 4)}var component = ActivatorUtilities.CreateInstance<{commandDefinition.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}>(context.Services);
{new string(' ', indent * 4)}((ICommandLineComponentInternals)component).Initialize(context);");

            sourceBuilder.Append(propertyAssignmentsBuilder);

            sourceBuilder.AppendLine($@"{new string(' ', indent * 4)}await component.{commandDefinition.ExecuteMethod.Identifier.ValueText}();
{new string(' ', indent * 4)}if (component is IAsyncDisposable asyncDisposable)
{new string(' ', (indent + 1) * 4)}await asyncDisposable.DisposeAsync();
{new string(' ', indent * 4)}if (component is IDisposable disposable)
{new string(' ', (indent + 1) * 4)}disposable.Dispose();");

            indent--;
            sourceBuilder.Append($@"{new string(' ', indent * 4)}}})");
        }
        if (hasElement)
            sourceBuilder.AppendLine(";");

        indent --;

        foreach (var nestedCommandName in commandDefinition.NestedCommands)
        {
            var nestedCommand = _componentClassSyntaxReceiver.CommandDefinitions[nestedCommandName];

            GenerateComponentBuilder(context, sourceBuilder, commandParameterName, nestedCommand.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), indent);
        }

        indent--;
        sourceBuilder.AppendLine($@"{new string(' ', indent * 4)}}});");
    }

    private string CompilerSafeString(string value)
    {
        var builder = new StringBuilder("\"", value.Length + 2);
        foreach (var valueChar in value)
            builder.Append(valueChar switch
            {
                '"' => "\\\"",
                '\n' => "\\\n",
                '\r' => "\\\r",
                '\t' => "\\\t",
                _ => valueChar
            });
        builder.Append("\"");
        return builder.ToString();
    }

    private string CompilerSafeVariableName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var valueChar in value)
        {
            if (!char.IsLetterOrDigit(valueChar))
                continue;
            builder.Append(valueChar);
        }
        if (char.IsDigit(builder[0]))
            builder.Insert(0, '_');
        return builder.ToString();
    }

}
