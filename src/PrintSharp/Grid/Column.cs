using PrintSharp.Styles;

namespace PrintSharp.Grid;

/// <summary>
/// 表示网格中的一列。
/// </summary>
public sealed class Column
{
    private int _index;
    private float _width = 80f;

    /// <summary>
    /// 获取或设置列的 0 基索引。
    /// </summary>
    public int Index
    {
        get => _index;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _index = value;
        }
    }

    /// <summary>
    /// 获取或设置列宽（单位：PrintSharp 逻辑网格单位，默认为 80）。
    /// </summary>
    public float Width
    {
        get => _width;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _width = value;
        }
    }

    /// <summary>
    /// 获取或设置该列单元格的默认样式（可选）。
    /// </summary>
    public CellStyle? DefaultStyle { get; set; }

    /// <summary>
    /// 获取或设置该列是否隐藏。
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// 初始化 <see cref="Column"/> 类的新实例。
    /// </summary>
    public Column() { }

    /// <summary>
    /// 初始化 <see cref="Column"/> 类的新实例。
    /// </summary>
    /// <param name="index">0 基列索引。</param>
    /// <param name="width">逻辑列宽。</param>
    /// <param name="defaultStyle">默认列样式。</param>
    public Column(int index, float width = 80f, CellStyle? defaultStyle = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        _index = index;
        _width = width;
        DefaultStyle = defaultStyle;
    }
}
