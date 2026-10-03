using PrintSharp.Styles;

namespace PrintSharp.Grid;

/// <summary>
/// 表示网格中的一行。
/// </summary>
public sealed class Row
{
    private int _index;
    private float _height = 20f;

    /// <summary>
    /// 获取或设置行的 0 基索引。
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
    /// 获取或设置行高（单位：PrintSharp 逻辑网格单位，默认为 20）。
    /// </summary>
    public float Height
    {
        get => _height;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _height = value;
        }
    }

    /// <summary>
    /// 获取或设置该行单元格的默认样式（可选）。
    /// </summary>
    public CellStyle? DefaultStyle { get; set; }

    /// <summary>
    /// 获取或设置该行是否隐藏。
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// 初始化 <see cref="Row"/> 类的新实例。
    /// </summary>
    public Row() { }

    /// <summary>
    /// 初始化 <see cref="Row"/> 类的新实例。
    /// </summary>
    /// <param name="index">0 基行索引。</param>
    /// <param name="height">逻辑行高。</param>
    /// <param name="defaultStyle">默认行样式。</param>
    public Row(int index, float height = 20f, CellStyle? defaultStyle = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        _index = index;
        _height = height;
        DefaultStyle = defaultStyle;
    }
}
