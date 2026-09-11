using Microsoft.CodeAnalysis;

namespace CoreVar.CommandLineInterface.SourceGenerator;

public class CommandArgumentSpec
{
    public bool PromptIfMissing { get; set; }

    public string? PromptLabel { get; set; }

    public bool Secret { get; set; }



    public string Name { get; set; } = default!;

    public string? Description { get; set; }
    
    public int? Index { get; set; }
    
    public string TargetPropertyName { get; set; } = default!;

    public ITypeSymbol TargetPropertyType { get; set; } = default!;

    public bool IsRequired { get; internal set; }

    public bool IsVariadic { get; set; }

    public System.Collections.Generic.List<string> Completions { get; } = new();
    public int ParameterIndex { get; set; } = -1;
}
