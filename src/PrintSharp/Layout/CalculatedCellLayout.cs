using PrintSharp.Cells;
using PrintSharp.Styles;

namespace PrintSharp.Layout;

/// <summary>
/// 单元格经过布局引擎计算后的物理排版结果。
/// </summary>
public sealed class CalculatedCellLayout
{
    /// <summary>
    /// 获取原始单元格模型定义。
    /// </summary>
    public Cell Cell { get; }

    /// <summary>
    /// 获取单元格在页面中的外部边框矩形（包含 Padding 和 Border）。
    /// </summary>
    public LayoutRect Bounds { get; }

    /// <summary>
    /// 获取单元格内部可用内容的实际排版矩形（去除 Padding 后的有效区域）。
    /// </summary>
    public LayoutRect ContentBounds { get; }

    /// <summary>
    /// 获取合并了行默认样式、列默认样式与单元格自定义样式后的最终有效样式。
    /// </summary>
    public CellStyle EffectiveStyle { get; }

    /// <summary>
    /// 初始化 <see cref="CalculatedCellLayout"/> 类的新实例。
    /// </summary>
    public CalculatedCellLayout(
        Cell cell,
        LayoutRect bounds,
        LayoutRect contentBounds,
        CellStyle effectiveStyle)
    {
        Cell = cell;
        Bounds = bounds;
        ContentBounds = contentBounds;
        EffectiveStyle = effectiveStyle;
    }

    /// <inheritdoc/>
    public override string ToString() => $"Layout[{Cell.Address}]: {Bounds}";
}
