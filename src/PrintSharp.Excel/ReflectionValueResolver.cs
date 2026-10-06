using System.Reflection;

namespace PrintSharp.Excel;

/// <summary>
/// 動的な POCO との互換性のため、公開インスタンス プロパティとフィールドを Reflection で解決します。
/// </summary>
public sealed class ReflectionValueResolver : ITemplateValueResolver, ICompiledTemplateValueResolver
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
        if (data is null)
        {
            return null;
        }

        var type = data.GetType();
        var property = type.GetProperty(segment,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is not null)
        {
            return property.GetValue(data);
        }

        var field = type.GetField(segment,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return field?.GetValue(data);
    }
}
