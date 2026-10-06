using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace PrintSharp.Excel.SourceGenerator;

[Generator]
public sealed class TemplateBindingsGenerator : IIncrementalGenerator
{
    private const string AttributeName = "PrintSharp.Excel.GenerateTemplateBindingsAttribute";

    private static readonly DiagnosticDescriptor GenericTypeNotSupported = new(
        "PSEXL001",
        "Generic template type is not supported",
        "Template bindings cannot be generated for generic type '{0}'",
        "PrintSharp.Excel.SourceGenerator",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var targetTypes = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeName,
            static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.TypeDeclarationSyntax,
            static (attributeContext, _) => attributeContext.TargetSymbol as INamedTypeSymbol)
            .Where(static symbol => symbol is not null);

        context.RegisterSourceOutput(targetTypes, static (productionContext, symbol) =>
        {
            if (symbol is null)
            {
                return;
            }

            if (symbol.IsGenericType || symbol.ContainingType is not null && symbol.ContainingType.IsGenericType)
            {
                productionContext.ReportDiagnostic(Diagnostic.Create(
                    GenericTypeNotSupported,
                    symbol.Locations.FirstOrDefault(),
                    symbol.ToDisplayString()));
                return;
            }

            var source = ResolverEmitter.Emit(symbol);
            productionContext.AddSource(ResolverEmitter.GetHintName(symbol), SourceText.From(source, Encoding.UTF8));
        });
    }
}

internal static class ResolverEmitter
{
    private const int MaxDepth = 5;

    public static string GetHintName(INamedTypeSymbol rootType)
    {
        var fullyQualifiedName = rootType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var hintName = new StringBuilder(fullyQualifiedName.Length);
        foreach (var character in fullyQualifiedName)
        {
            hintName.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return hintName + ".TemplateValueResolver.g.cs";
    }

    public static string Emit(INamedTypeSymbol rootType)
    {
        var handlers = new List<TypeHandler>();
        AddHandler(rootType, handlers, new HashSet<string>(), 0);

        var namespaceName = rootType.ContainingNamespace.IsGlobalNamespace
            ? null
            : rootType.ContainingNamespace.ToDisplayString();
        var resolverName = GetResolverName(rootType);
        var builder = new StringBuilder();
        builder.AppendLine("#nullable enable");
        if (namespaceName is not null)
        {
            builder.Append("namespace ").Append(namespaceName).AppendLine(";");
        }

        builder.AppendLine("/// <summary>テンプレート データへの直接アクセス用に生成された Resolver です。</summary>");
        builder.Append("public sealed class ").Append(resolverName)
            .AppendLine(" : global::PrintSharp.Excel.ITemplateValueResolver");
        builder.AppendLine("{");
        builder.AppendLine("    /// <summary>生成対象の型に対応する Binding Path を解決します。</summary>");
        builder.AppendLine("    /// <param name=\"data\">テンプレート データです。</param>");
        builder.AppendLine("    /// <param name=\"path\">解決する Binding Path です。</param>");
        builder.AppendLine("    /// <returns>解決した値、または該当しない場合は null です。</returns>");
        builder.AppendLine("    public object? Resolve(object? data, string path)");
        builder.AppendLine("    {");
        builder.AppendLine("        if (data is null || string.IsNullOrWhiteSpace(path)) return null;");

        foreach (var handler in handlers)
        {
            var typeName = FormatType(handler.Type);
            var variableName = "typedData" + handlers.IndexOf(handler);
            builder.Append("        if (data is ").Append(typeName).Append(' ').Append(variableName).AppendLine(")");
            builder.AppendLine("        {");
            foreach (var entry in handler.Entries)
            {
                var path = entry.Path.Replace("\\", "\\\\").Replace("\"", "\\\"");
                var expression = BuildAccessExpression(variableName, handler.Type, entry.Members, 0);
                builder.Append("            if (global::System.String.Equals(path, \"")
                    .Append(path)
                    .Append("\", global::System.StringComparison.OrdinalIgnoreCase)) return (object?)(")
                    .Append(expression).AppendLine(");");
            }
            builder.AppendLine("            return null;");
            builder.AppendLine("        }");
        }

        builder.AppendLine("        return null;");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        builder.Append("internal static class ").Append(resolverName).AppendLine("Registration");
        builder.AppendLine("{");
        builder.AppendLine("    [global::System.Runtime.CompilerServices.ModuleInitializer]");
        builder.AppendLine("    internal static void RegisterResolver()");
        builder.AppendLine("    {");
        builder.Append("        global::PrintSharp.Excel.TemplateValueResolverRegistry<")
            .Append(FormatType(rootType)).Append(">.Register(new ").Append(resolverName).AppendLine("());");
        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static void AddHandler(
        INamedTypeSymbol type,
        List<TypeHandler> handlers,
        HashSet<string> visited,
        int depth)
    {
        var key = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (depth > MaxDepth || !visited.Add(key) || IsSimpleType(type))
        {
            return;
        }

        var handler = new TypeHandler(type);
        handlers.Add(handler);
        CollectEntries(type, string.Empty, new List<ISymbol>(), handler.Entries, handlers, visited, depth);
    }

    private static void CollectEntries(
        ITypeSymbol currentType,
        string prefix,
        List<ISymbol> members,
        List<BindingEntry> entries,
        List<TypeHandler> handlers,
        HashSet<string> visited,
        int depth)
    {
        if (depth >= MaxDepth || currentType is not INamedTypeSymbol namedType)
        {
            return;
        }

        foreach (var member in GetBindableMembers(namedType))
        {
            var memberType = GetMemberType(member);
            var path = string.IsNullOrEmpty(prefix) ? member.Name : prefix + "." + member.Name;
            var memberPath = new List<ISymbol>(members) { member };
            entries.Add(new BindingEntry(path, memberPath));

            if (TryGetCollectionElementType(memberType, out var elementType))
            {
                if (elementType is INamedTypeSymbol collectionElement && !IsSimpleType(collectionElement))
                {
                    AddHandler(collectionElement, handlers, visited, depth + 1);
                }
                continue;
            }

            var nestedType = UnwrapNullable(memberType) as INamedTypeSymbol;
            if (nestedType is not null && !IsSimpleType(nestedType))
            {
                CollectEntries(nestedType, path, memberPath, entries, handlers, visited, depth + 1);
            }
        }
    }

    private static IEnumerable<ISymbol> GetBindableMembers(INamedTypeSymbol type)
    {
        var memberNames = new HashSet<string>(StringComparer.Ordinal);
        for (var currentType = type;
             currentType is not null && currentType.SpecialType != SpecialType.System_Object;
             currentType = currentType.BaseType)
        {
            foreach (var property in currentType.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.DeclaredAccessibility == Accessibility.Public &&
                    !property.IsStatic && !property.IsIndexer && memberNames.Add(property.Name) &&
                    property.GetMethod is { DeclaredAccessibility: Accessibility.Public })
                {
                    yield return property;
                }
            }

            foreach (var field in currentType.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.DeclaredAccessibility == Accessibility.Public && !field.IsStatic && !field.IsConst && memberNames.Add(field.Name))
                {
                    yield return field;
                }
            }
        }
    }

    private static ITypeSymbol GetMemberType(ISymbol member) => member switch
    {
        IPropertySymbol property => property.Type,
        IFieldSymbol field => field.Type,
        _ => throw new InvalidOperationException()
    };

    private static string BuildAccessExpression(
        string expression,
        ITypeSymbol currentType,
        IReadOnlyList<ISymbol> members,
        int memberIndex)
    {
        if (memberIndex >= members.Count)
        {
            return expression;
        }

        if (IsNullableValueType(currentType))
        {
            var valueExpression = expression + ".Value";
            var next = AppendMember(valueExpression, members[memberIndex]);
            return expression + ".HasValue ? " + BuildAccessExpression(next, GetMemberType(members[memberIndex]), members, memberIndex + 1) + " : null";
        }

        if (currentType.IsReferenceType && memberIndex > 0)
        {
            var next = AppendMember(expression, members[memberIndex]);
            var nextType = GetMemberType(members[memberIndex]);
            return expression + " is null ? null : " + BuildAccessExpression(next, nextType, members, memberIndex + 1);
        }

        var access = AppendMember(expression, members[memberIndex]);
        return BuildAccessExpression(access, GetMemberType(members[memberIndex]), members, memberIndex + 1);
    }

    private static string AppendMember(string expression, ISymbol member) => expression + "." + EscapeIdentifier(member.Name);

    private static bool TryGetCollectionElementType(ITypeSymbol type, out ITypeSymbol? elementType)
    {
        type = UnwrapNullable(type);
        if (type is IArrayTypeSymbol arrayType)
        {
            elementType = arrayType.ElementType;
            return true;
        }

        if (type is INamedTypeSymbol namedType)
        {
            var enumerableType = namedType.AllInterfaces
                .Concat(new[] { namedType })
                .FirstOrDefault(candidate => candidate.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>");
            if (enumerableType is not null && enumerableType.TypeArguments.Length == 1)
            {
                elementType = enumerableType.TypeArguments[0];
                return true;
            }
        }

        elementType = null;
        return false;
    }

    private static bool IsSimpleType(ITypeSymbol type)
    {
        type = UnwrapNullable(type);
        return type.SpecialType != SpecialType.None ||
            type.TypeKind == TypeKind.Enum ||
            type.ToDisplayString().StartsWith("System.", StringComparison.Ordinal);
    }

    private static bool IsNullableValueType(ITypeSymbol type) =>
        type is INamedTypeSymbol namedType && namedType.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    private static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
        IsNullableValueType(type) ? ((INamedTypeSymbol)type).TypeArguments[0] : type;

    private static string FormatType(ITypeSymbol type) =>
        type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string EscapeIdentifier(string name) => SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    private static string GetResolverName(INamedTypeSymbol rootType)
    {
        var typeNames = new Stack<string>();
        for (var current = rootType; current is not null; current = current.ContainingType)
        {
            typeNames.Push(current.Name);
        }

        return string.Join("_", typeNames) + "TemplateValueResolver";
    }

    private sealed class TypeHandler(INamedTypeSymbol type)
    {
        public INamedTypeSymbol Type { get; } = type;
        public List<BindingEntry> Entries { get; } = new();
    }

    private sealed class BindingEntry(string path, List<ISymbol> members)
    {
        public string Path { get; } = path;
        public List<ISymbol> Members { get; } = members;
    }
}
