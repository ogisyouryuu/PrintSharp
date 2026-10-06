namespace PrintSharp.Excel;

/// <summary>
/// テンプレート データからドット区切りのパスで値を解決します。
/// </summary>
public interface ITemplateValueResolver
{
    /// <summary>
    /// <paramref name="data"/> から <paramref name="path"/> で指定された値を解決します。
    /// </summary>
    /// <param name="data">テンプレート データのルート オブジェクト。</param>
    /// <param name="path"><c>Customer.Name</c> のようなプロパティ パス。</param>
    /// <returns>解決した値。パスが存在しない場合、または途中の値が null の場合は <see langword="null"/>。</returns>
    object? Resolve(object? data, string path);
}
