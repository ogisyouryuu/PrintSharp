namespace PrintSharp.Excel;

/// <summary>
/// Resolves a value from a template data object using a dotted property path.
/// </summary>
public interface ITemplateValueResolver
{
    /// <summary>
    /// Resolves the value identified by <paramref name="path"/> from <paramref name="data"/>.
    /// </summary>
    /// <param name="data">The root template data object.</param>
    /// <param name="path">A property path such as <c>Customer.Name</c>.</param>
    /// <returns>The resolved value, or <see langword="null"/> when the path is missing or a segment is null.</returns>
    object? Resolve(object? data, string path);
}
