using Microsoft.CodeAnalysis;

namespace CoreVar.CommandLineInterface.SourceGenerator;

public class CommandOptionSpec
{
    public string Name { get; set; } = default!;

    public string TargetPropertyName { get; set; } = default!;

    public ITypeSymbol TargetPropertyType { get; set; } = default!;
    
    public string? Description { get; set; }
    
    public bool IsRequired { get; set; }
}
