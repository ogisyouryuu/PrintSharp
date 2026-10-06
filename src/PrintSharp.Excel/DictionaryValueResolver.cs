using System.Collections;

namespace PrintSharp.Excel;

/// <summary>
/// Resolves dotted paths in generic and non-generic dictionaries without reflection.
/// </summary>
public sealed class DictionaryValueResolver : ITemplateValueResolver, ICompiledTemplateValueResolver
{
    /// <inheritdoc />
    public object? Resolve(object? data, string path)
    {
        if (data is null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Resolve(data, path.Split('.'));
    }

    object? ICompiledTemplateValueResolver.Resolve(object? data, TemplateBinding binding)
    {
        return Resolve(data, binding.Segments);
    }

    private static object? Resolve(object? data, IReadOnlyList<string> segments)
    {
        object? current = data;
        foreach (var part in segments)
        {
            current = ResolveSegment(current, part);
        }

        return current;
    }

    internal static object? ResolveSegment(object? data, string segment)
    {
        if (data is IDictionary<string, object?> genericDictionary)
        {
            genericDictionary.TryGetValue(segment, out var value);
            return value;
        }

        if (data is IDictionary dictionary)
        {
            return dictionary.Contains(segment) ? dictionary[segment] : null;
        }

        return null;
    }
}

internal sealed class DefaultTemplateValueResolver : ITemplateValueResolver, ICompiledTemplateValueResolver
{
    public object? Resolve(object? data, string path)
    {
        if (data is null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Resolve(data, path.Split('.'));
    }

    object? ICompiledTemplateValueResolver.Resolve(object? data, TemplateBinding binding)
    {
        return Resolve(data, binding.Segments);
    }

    private object? Resolve(object? data, IReadOnlyList<string> segments)
    {
        object? current = data;
        foreach (var part in segments)
        {
            if (current is null)
            {
                return null;
            }

            current = current is IDictionary<string, object?> or IDictionary
                ? DictionaryValueResolver.ResolveSegment(current, part)
                : ReflectionValueResolver.ResolveSegment(current, part);
        }

        return current;
    }
}
