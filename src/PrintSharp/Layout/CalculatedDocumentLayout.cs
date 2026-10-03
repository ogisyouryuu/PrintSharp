using PrintSharp.Documents;

namespace PrintSharp.Layout;

/// <summary>
/// 整个网格文档经过布局引擎计算后的排版结果。
/// </summary>
public sealed class CalculatedDocumentLayout
{
    /// <summary>
    /// 获取原始网格文档定义。
    /// </summary>
    public Document Document { get; }

    /// <summary>
    /// 获取所有页面的排版计算结果。
    /// </summary>
    public IReadOnlyList<CalculatedPageLayout> Pages { get; }

    /// <summary>
    /// 获取默认页面的排版计算结果。
    /// </summary>
    public CalculatedPageLayout DefaultPage => Pages[0];

    /// <summary>
    /// 初始化 <see cref="CalculatedDocumentLayout"/> 类的新实例。
    /// </summary>
    public CalculatedDocumentLayout(Document document, IReadOnlyList<CalculatedPageLayout> pages)
    {
        Document = document;
        Pages = pages;
    }
}
