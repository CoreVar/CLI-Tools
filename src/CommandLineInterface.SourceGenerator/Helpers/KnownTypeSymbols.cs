using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreVar.CommandLineInterface.SourceGenerator.Helpers;

internal sealed class KnownTypeSymbols(Compilation compilation)
{

    public Compilation Compilation => compilation;

    public INamedTypeSymbol ComponentContextType => _componentContextType.Value;
    private readonly Lazy<INamedTypeSymbol> _componentContextType = new(() => compilation.GetBestTypeByMetadataName("CoreVar.CommandLineInterface.ComponentContext")!);

    public INamedTypeSymbol ComponentAttributeType => _componentAttributeType.Value;
    private readonly Lazy<INamedTypeSymbol> _componentAttributeType = new(() => compilation.GetBestTypeByMetadataName("CoreVar.CommandLineInterface.ComponentAttribute`1")!);

    public INamedTypeSymbol CommandNameAttributeType => _commandNameAttributeType.Value;
    private readonly Lazy<INamedTypeSymbol> _commandNameAttributeType = new(() => compilation.GetBestTypeByMetadataName("CoreVar.CommandLineInterface.CommandNameAttribute")!);

    public INamedTypeSymbol DescriptionAttributeType => _descriptionAttributeType.Value;
    private readonly Lazy<INamedTypeSymbol> _descriptionAttributeType = new(() => compilation.GetBestTypeByMetadataName("CoreVar.CommandLineInterface.DescriptionAttribute")!);

    public INamedTypeSymbol CommandOptionAttributeType => _commandOptionAttributeType.Value;
    private readonly Lazy<INamedTypeSymbol> _commandOptionAttributeType = new(() => compilation.GetBestTypeByMetadataName("CoreVar.CommandLineInterface.CommandOptionAttribute")!);

    public INamedTypeSymbol CommandArgumentAttributeType => _commandArgumentAttributeType.Value;
    private readonly Lazy<INamedTypeSymbol> _commandArgumentAttributeType = new(() => compilation.GetBestTypeByMetadataName("CoreVar.CommandLineInterface.CommandArgumentAttribute")!);

    public INamedTypeSymbol RequiredAttributeType => _requiredAttributeType.Value;
    private readonly Lazy<INamedTypeSymbol> _requiredAttributeType = new(() => compilation.GetBestTypeByMetadataName("CoreVar.CommandLineInterface.RequiredAttribute")!);

    public INamedTypeSymbol OptionalAttributeType => _optionalAttributeType.Value;
    private readonly Lazy<INamedTypeSymbol> _optionalAttributeType = new(() => compilation.GetBestTypeByMetadataName("CoreVar.CommandLineInterface.OptionalAttribute")!);

}
