using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace CoreVar.CommandLineInterface.SourceGenerator;

public class ComponentContextSpec
{

    public INamedTypeSymbol Type { get; set; } = default!;

    public ImmutableArray<ComponentSpec> Components { get; set; } = default!;

}
