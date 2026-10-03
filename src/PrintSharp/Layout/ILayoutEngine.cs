using PrintSharp.Documents;

namespace PrintSharp.Layout;

/// <summary>
/// 定义文档网格布局计算引擎接口。
/// 负责将抽象网格定义（Row、Column、Span）转换为确定的逻辑物理矩形（LayoutRect）。
/// </summary>
public interface ILayoutEngine
{
    /// <summary>
    /// 计算指定页面的物理布局。
    /// </summary>
    /// <param name="page">待计算的页面。</param>
    /// <returns>页面的排版计算结果。</returns>
    CalculatedPageLayout CalculatePage(Page page);

    /// <summary>
    /// 计算整个文档的物理布局。
    /// </summary>
    /// <param name="document">待计算的文档。</param>
    /// <returns>文档的排版计算结果。</returns>
    CalculatedDocumentLayout CalculateDocument(Document document);
}
