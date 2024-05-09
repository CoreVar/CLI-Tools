using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace CoreVar.CommandLineInterface.SourceGenerator;

public class CommandDefinition
{

    public ISymbol Type { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public List<string> NestedCommands { get; } = [];

    public List<CommandOptionDefinition> Options { get; } = [];

    public List<CommandArgumentDefinition> Arguments { get; } = [];

    public MethodDeclarationSyntax? ExecuteMethod { get; set; }
    
    public MethodDeclarationSyntax? SetupHostBuilderMethod { get; set; }
    
    public MethodDeclarationSyntax? SetupHostMethod { get; set; }

    public MethodDeclarationSyntax? SetupServicesMethod { get; set; }
}
