using PrintSharp.Cells;
using PrintSharp.Documents;

namespace PrintSharp.Excel;

internal sealed class TemplateBinding(string path, int bindingId, string[] segments)
{
    public string Path { get; } = path;
    public int BindingId { get; } = bindingId;
    public string[] Segments { get; } = segments;
}

internal interface ICompiledTemplateValueResolver
{
    object? Resolve(object? data, TemplateBinding binding);

    bool TryResolveCollection(object? data, TemplateBinding binding, out IEnumerable<object> items)
    {
        var value = Resolve(data, binding);
        if (DataSetTemplateValueResolver.TryGetRows(value, out items))
        {
            return true;
        }

        if (value is System.Collections.IEnumerable enumerable and not string &&
            value is not System.Collections.Generic.IDictionary<string, object?> &&
            value is not System.Collections.IDictionary)
        {
            items = Enumerate(enumerable);
            return true;
        }

        items = Array.Empty<object>();
        return false;
    }

    private static IEnumerable<object> Enumerate(System.Collections.IEnumerable source)
    {
        foreach (var item in source)
        {
            if (item is not null)
            {
                yield return item;
            }
        }
    }
}

internal sealed class CompiledTemplate(Document document, IReadOnlyList<CompiledPage> pages)
{
    public Document Document { get; } = document;
    public IReadOnlyList<CompiledPage> Pages { get; } = pages;
}

internal sealed class CompiledPage(Page page, IReadOnlyDictionary<int, IReadOnlyList<CompiledCell>> rows)
{
    public Page Page { get; } = page;
    public IReadOnlyDictionary<int, IReadOnlyList<CompiledCell>> Rows { get; } = rows;
    public int MaxRow { get; } = rows.Count == 0 ? -1 : rows.Keys.Max();
}

internal sealed class CompiledCell(
    Cell cell,
    TemplateBinding? singleBinding,
    TemplateBinding? singleCollectionRootBinding,
    TemplateBinding? singleItemBinding,
    IReadOnlyList<CompiledTemplatePart> parts)
{
    public Cell Cell { get; } = cell;
    public TemplateBinding? SingleBinding { get; } = singleBinding;
    public TemplateBinding? SingleCollectionRootBinding { get; } = singleCollectionRootBinding;
    public TemplateBinding? SingleItemBinding { get; } = singleItemBinding;
    public IReadOnlyList<CompiledTemplatePart> Parts { get; } = parts;
    public bool HasPlaceholders { get; } = singleBinding is not null || parts.Any(part => part.Binding is not null);
}

internal readonly record struct CompiledTemplatePart(
    string? Literal,
    TemplateBinding? Binding,
    TemplateBinding? CollectionRootBinding,
    TemplateBinding? ItemBinding);

internal sealed class TemplateBindingRegistry
{
    private readonly Dictionary<string, TemplateBinding> _bindings = new(StringComparer.Ordinal);

    public TemplateBinding GetOrAdd(string path)
    {
        if (_bindings.TryGetValue(path, out var binding))
        {
            return binding;
        }

        var segments = path.Split('.');
        binding = new TemplateBinding(path, _bindings.Count, segments);
        _bindings.Add(path, binding);
        return binding;
    }
}
