using System.Collections;
using System.Collections.Generic;

namespace PrintSharp.Excel;

internal sealed class CompositeTemplateValueResolver(ITemplateValueResolver primary)
    : ITemplateValueResolver, ICompiledTemplateValueResolver
{
    public object? Resolve(object? data, string path)
    {
        var value = primary.Resolve(data, path);
        if (value is not null || data is null || string.IsNullOrWhiteSpace(path))
        {
            return value;
        }

        return DataSetTemplateValueResolver.ResolveFallback(data, new TemplateBinding(path, -1, path.Split('.')));
    }

    object? ICompiledTemplateValueResolver.Resolve(object? data, TemplateBinding binding)
    {
        var value = primary is ICompiledTemplateValueResolver compiled
            ? compiled.Resolve(data, binding)
            : primary.Resolve(data, binding.Path);
        return value ?? DataSetTemplateValueResolver.ResolveFallback(data, binding);
    }

    bool ICompiledTemplateValueResolver.TryResolveCollection(
        object? data,
        TemplateBinding binding,
        out IEnumerable<object> items)
    {
        if (primary is ICompiledTemplateValueResolver compiled &&
            compiled.TryResolveCollection(data, binding, out items))
        {
            return true;
        }

        var value = primary is ICompiledTemplateValueResolver compiledResolver
            ? compiledResolver.Resolve(data, binding)
            : primary.Resolve(data, binding.Path);
        value ??= DataSetTemplateValueResolver.ResolveFallback(data, binding);
        if (DataSetTemplateValueResolver.TryGetRows(value, out items))
        {
            return true;
        }

        if (value is IEnumerable enumerable and not string &&
            value is not IDictionary<string, object?> && value is not IDictionary)
        {
            items = Enumerate(enumerable);
            return true;
        }

        items = Array.Empty<object>();
        return false;
    }

    private static IEnumerable<object> Enumerate(IEnumerable source)
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
