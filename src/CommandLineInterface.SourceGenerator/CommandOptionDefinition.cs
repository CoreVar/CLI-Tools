using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreVar.CommandLineInterface.SourceGenerator;

public class CommandOptionDefinition
{
    public string Name { get; set; } = default!;

    public PropertyDeclarationSyntax TargetProperty { get; set; } = default!;
    
    public string? Description { get; set; }
    
    public bool IsRequired { get; set; }
}
