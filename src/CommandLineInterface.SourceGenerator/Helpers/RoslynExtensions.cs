using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace CoreVar.CommandLineInterface.SourceGenerator.Helpers;

public static class RoslynExtensions
{

    //public static INamedTypeSymbol? GetBestTypeByMetadataName(this Compilation compilation, Type type)
    //{
    //    Debug.Assert(!type.IsArray, "Resolution logic only capable of handling named types.");
    //    Debug.Assert(type.FullName != null);
    //    return compilation.GetBestTypeByMetadataName(type.FullName);
    //}

    public static INamedTypeSymbol? GetBestTypeByMetadataName(this Compilation compilation, string fullyQualifiedMetadataName)
    {
        // Try to get the unique type with this name, ignoring accessibility
        var type = compilation.GetTypeByMetadataName(fullyQualifiedMetadataName);

        // Otherwise, try to get the unique type with this name originally defined in 'compilation'
        type ??= compilation.Assembly.GetTypeByMetadataName(fullyQualifiedMetadataName);

        // Otherwise, try to get the unique accessible type with this name from a reference
        if (type is null)
        {
            foreach (var module in compilation.Assembly.Modules)
            {
                foreach (var referencedAssembly in module.ReferencedAssemblySymbols)
                {
                    var currentType = referencedAssembly.GetTypeByMetadataName(fullyQualifiedMetadataName);
                    if (currentType is null)
                        continue;
                        
                    switch (currentType.GetResultantVisibility())
                    {
                        case SymbolVisibility.Public:
                        case SymbolVisibility.Internal when referencedAssembly.GivesAccessTo(compilation.Assembly):
                            break;

                        default:
                            continue;
                    }

                    if (type is object)
                    {
                        // Multiple visible types with the same metadata name are present
                        return null;
                    }

                    type = currentType;
                }
            }
        }

        return type;
    }

    // copied from https://github.com/dotnet/roslyn/blob/main/src/Workspaces/SharedUtilitiesAndExtensions/Compiler/Core/Extensions/ISymbolExtensions.cs
    private static SymbolVisibility GetResultantVisibility(this ISymbol symbol)
    {
        // Start by assuming it's visible.
        SymbolVisibility visibility = SymbolVisibility.Public;

        switch (symbol.Kind)
        {
            case SymbolKind.Alias:
                // Aliases are uber private.  They're only visible in the same file that they
                // were declared in.
                return SymbolVisibility.Private;

            case SymbolKind.Parameter:
                // Parameters are only as visible as their containing symbol
                return GetResultantVisibility(symbol.ContainingSymbol);

            case SymbolKind.TypeParameter:
                // Type Parameters are private.
                return SymbolVisibility.Private;
        }

        while (symbol != null && symbol.Kind != SymbolKind.Namespace)
        {
            switch (symbol.DeclaredAccessibility)
            {
                // If we see anything private, then the symbol is private.
                case Accessibility.NotApplicable:
                case Accessibility.Private:
                    return SymbolVisibility.Private;

                // If we see anything internal, then knock it down from public to
                // internal.
                case Accessibility.Internal:
                case Accessibility.ProtectedAndInternal:
                    visibility = SymbolVisibility.Internal;
                    break;

                    // For anything else (Public, Protected, ProtectedOrInternal), the
                    // symbol stays at the level we've gotten so far.
            }

            symbol = symbol.ContainingSymbol;
        }

        return visibility;
    }

    // Copied from: https://github.com/dotnet/roslyn/blob/main/src/Workspaces/SharedUtilitiesAndExtensions/Compiler/Core/Utilities/SymbolVisibility.cs
    private enum SymbolVisibility
    {
        Public = 0,
        Internal = 1,
        Private = 2,
        Friend = Internal,
    }

    public static bool IsAssignableFrom(this ITypeSymbol? baseType, ITypeSymbol? type)
    {
        if (baseType is null || type is null)
        {
            return false;
        }

        if (baseType.TypeKind is TypeKind.Interface)
        {
            if (type.AllInterfaces.Contains(baseType, SymbolEqualityComparer.Default))
            {
                return true;
            }
        }

        for (INamedTypeSymbol? current = type as INamedTypeSymbol; current != null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(baseType, current))
            {
                return true;
            }
        }

        return false;
    }

}
