using System.Reflection;

namespace PrintSharp.Excel;

/// <summary>
/// Resolves public instance properties and fields for dynamic POCO compatibility.
/// </summary>
public sealed class ReflectionValueResolver : ITemplateValueResolver
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
            if (current is null)
            {
                return null;
            }

            var type = current.GetType();
            var property = type.GetProperty(part,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (property is not null)
            {
                current = property.GetValue(current);
                continue;
            }

            var field = type.GetField(part,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            current = field?.GetValue(current);
        }

        return current;
    }
}
