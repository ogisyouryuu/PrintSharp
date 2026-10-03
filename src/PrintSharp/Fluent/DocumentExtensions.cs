using PrintSharp.Documents;
using PrintSharp.Layout;

namespace PrintSharp.Fluent;

/// <summary>
/// 为 <see cref="Document"/> 及 <see cref="Page"/> 提供的便捷扩展方法。
/// </summary>
public static class DocumentExtensions
{
    /// <summary>
    /// 计算指定文档的物理排版结果。
    /// </summary>
    /// <param name="document">网格文档。</param>
    /// <param name="engine">自定义布局引擎，若为 null 则使用默认 <see cref="GridLayoutEngine"/>。</param>
    /// <returns>计算后的排版结果。</returns>
    public static CalculatedDocumentLayout CalculateLayout(this Document document, ILayoutEngine? engine = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        var layoutEngine = engine ?? GridLayoutEngine.Instance;
        return layoutEngine.CalculateDocument(document);
    }

    /// <summary>
    /// 计算指定页面的物理排版结果。
    /// </summary>
    /// <param name="page">网格页面。</param>
    /// <param name="engine">自定义布局引擎，若为 null 则使用默认 <see cref="GridLayoutEngine"/>。</param>
    /// <returns>计算后的页面排版结果。</returns>
    public static CalculatedPageLayout CalculateLayout(this Page page, ILayoutEngine? engine = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        var layoutEngine = engine ?? GridLayoutEngine.Instance;
        return layoutEngine.CalculatePage(page);
    }
}
