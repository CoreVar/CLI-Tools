using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreVar.CommandLineInterface.SourceGenerator;

public class CommandArgumentSpec
{

    public string Name { get; set; } = default!;

    public string? Description { get; set; }
    
    public int? Index { get; set; }
    
    public string TargetPropertyName { get; set; } = default!;

    public ITypeSymbol TargetPropertyType { get; set; } = default!;

    public bool IsRequired { get; internal set; }
}
