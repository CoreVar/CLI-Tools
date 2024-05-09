using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreVar.CommandLineInterface.SourceGenerator;

public class ComponentContextDefinition
{

    public ClassDeclarationSyntax ClassDeclaration { get; set; } = default!;

    public HashSet<string> Components { get; set; } = default!;
}
