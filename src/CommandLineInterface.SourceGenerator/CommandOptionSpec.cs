using Microsoft.CodeAnalysis;

namespace CoreVar.CommandLineInterface.SourceGenerator;

public class CommandOptionSpec
{
    public bool PromptIfMissing { get; set; }

    public string? PromptLabel { get; set; }

    public bool Secret { get; set; }


    public string Name { get; set; } = default!;

    public string TargetPropertyName { get; set; } = default!;

    public ITypeSymbol TargetPropertyType { get; set; } = default!;
    
    public string? Description { get; set; }
    
    public bool IsRequired { get; set; }

    public System.Collections.Generic.List<string> Aliases { get; } = new();

    public string? EnvironmentVariable { get; set; }
    public string? ConfigurationKey { get; set; }
    public bool IsGlobal { get; set; }
    public bool IsHidden { get; set; }
    public string? DeprecationMessage { get; set; }
    public System.Collections.Generic.List<string> Completions { get; } = new();
    public int ParameterIndex { get; set; } = -1;
}
