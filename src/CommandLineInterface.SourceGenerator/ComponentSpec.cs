using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace CoreVar.CommandLineInterface.SourceGenerator;

public class ComponentSpec
{

    public ISymbol Type { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string? Description { get; set; }

    public List<ComponentSpec> NestedCommands { get; } = [];

    public List<CommandOptionSpec> Options { get; } = [];

    public List<CommandArgumentSpec> Arguments { get; } = [];

    public string? ExecuteMethodName { get; set; }
    
    public string? SetupHostBuilderMethodName { get; set; }
    
    public string? SetupHostMethodName { get; set; }

    public string? SetupServicesMethodName { get; set; }

}
