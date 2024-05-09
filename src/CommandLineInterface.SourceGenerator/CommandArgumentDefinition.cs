using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreVar.CommandLineInterface.SourceGenerator;

public class CommandArgumentDefinition
{

    public string Name { get; set; } = default!;

    public string? Description { get; set; }
    
    public int? Index { get; set; }
    
    public PropertyDeclarationSyntax TargetProperty { get; set; } = default!;
    public bool IsRequired { get; internal set; }
}
