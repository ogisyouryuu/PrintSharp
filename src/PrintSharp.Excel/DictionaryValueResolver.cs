using System.Collections;

namespace PrintSharp.Excel;

/// <summary>
/// Resolves dotted paths in generic and non-generic dictionaries without reflection.
/// </summary>
public sealed class DictionaryValueResolver : ITemplateValueResolver
{
    /// <inheritdoc />
    public object? Resolve(object? data, string path)
    {
        if (data is null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        object? current = data;
        foreach (var part in path.Split('.'))
        {
            if (current is IDictionary<string, object?> genericDictionary)
            {
                genericDictionary.TryGetValue(part, out current);
                continue;
            }

            if (current is IDictionary dictionary)
            {
                current = dictionary.Contains(part) ? dictionary[part] : null;
                continue;
            }

            return null;
        }

        return current;
    }
}

internal sealed class DefaultTemplateValueResolver : ITemplateValueResolver
{
    private readonly DictionaryValueResolver _dictionaryResolver = new();
    private readonly ReflectionValueResolver _reflectionResolver = new();

    public object? Resolve(object? data, string path)
    {
        if (data is null || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        object? current = data;
        foreach (var part in path.Split('.'))
        {
            if (current is null)
            {
                return null;
            }

            current = current is IDictionary<string, object?> or IDictionary
                ? _dictionaryResolver.Resolve(current, part)
                : _reflectionResolver.Resolve(current, part);
        }

        return current;
    }
}
