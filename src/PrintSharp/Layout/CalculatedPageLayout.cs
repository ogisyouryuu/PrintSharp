using PrintSharp.Documents;

namespace PrintSharp.Layout;

/// <summary>
/// 页面经过网格布局引擎计算后的整体排版结果。
/// </summary>
public sealed class CalculatedPageLayout
{
    /// <summary>
    /// 获取原始页面模型定义。
    /// </summary>
    public Page Page { get; }

    /// <summary>该物理页所属的原始逻辑页面。</summary>
    public Page SourcePage => Page.Context?.SourcePage ?? Page;

    /// <summary>
    /// 获取网格内容区域的总逻辑宽度。
    /// </summary>
    public float ContentWidth { get; }

    /// <summary>
    /// 获取网格内容区域的总逻辑高度。
    /// </summary>
    public float ContentHeight { get; }

    /// <summary>
    /// 获取包含页边距后的整个页面总逻辑宽度。
    /// </summary>
    public float TotalWidth { get; }

    /// <summary>
    /// 获取包含页边距后的整个页面总逻辑高度。
    /// </summary>
    public float TotalHeight { get; }

    /// <summary>
    /// 获取所有单元格的计算结果集合。
    /// </summary>
    public IReadOnlyList<CalculatedCellLayout> Cells { get; }

    /// <summary>
    /// 初始化 <see cref="CalculatedPageLayout"/> 类的新实例。
    /// </summary>
    public CalculatedPageLayout(
        Page page,
        float contentWidth,
        float contentHeight,
        float totalWidth,
        float totalHeight,
        IReadOnlyList<CalculatedCellLayout> cells)
    {
        Page = page;
        ContentWidth = contentWidth;
        ContentHeight = contentHeight;
        TotalWidth = totalWidth;
        TotalHeight = totalHeight;
        Cells = cells;
    }

    /// <summary>
    /// 查找指定单元格坐标的布局计算结果。
    /// </summary>
    /// <param name="row">0 基行索引。</param>
    /// <param name="column">0 基列索引。</param>
    /// <returns>匹配的计算布局，未找到则返回 null。</returns>
    public CalculatedCellLayout? FindCell(int row, int column)
    {
        return Cells.FirstOrDefault(c => c.Cell.Row == row && c.Cell.Column == column);
    }
}
