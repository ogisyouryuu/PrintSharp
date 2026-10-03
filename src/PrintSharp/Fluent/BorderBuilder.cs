using PrintSharp.Styles;

namespace PrintSharp.Fluent;

/// <summary>
/// 单元格边框配置 Fluent 构建器。
/// </summary>
public sealed class BorderBuilder
{
    private BorderLine? _top;
    private BorderLine? _bottom;
    private BorderLine? _left;
    private BorderLine? _right;
    private BorderLine? _diagonal;

    /// <summary>
    /// 同时设置四周（上、下、左、右）边框。
    /// </summary>
    public BorderBuilder All(BorderStyle style, ColorSpec? color = null)
    {
        var line = new BorderLine(style, color);
        _top = line;
        _bottom = line;
        _left = line;
        _right = line;
        return this;
    }

    /// <summary>
    /// 同时设置四周（上、下、左、右）边框。
    /// </summary>
    public BorderBuilder All(BorderStyle style, string hexColor)
    {
        return All(style, ColorSpec.FromHex(hexColor));
    }

    /// <summary>
    /// 设置上边框。
    /// </summary>
    public BorderBuilder Top(BorderStyle style, ColorSpec? color = null)
    {
        _top = new BorderLine(style, color);
        return this;
    }

    /// <summary>
    /// 设置下边框。
    /// </summary>
    public BorderBuilder Bottom(BorderStyle style, ColorSpec? color = null)
    {
        _bottom = new BorderLine(style, color);
        return this;
    }

    /// <summary>
    /// 设置左边框。
    /// </summary>
    public BorderBuilder Left(BorderStyle style, ColorSpec? color = null)
    {
        _left = new BorderLine(style, color);
        return this;
    }

    /// <summary>
    /// 设置右边框。
    /// </summary>
    public BorderBuilder Right(BorderStyle style, ColorSpec? color = null)
    {
        _right = new BorderLine(style, color);
        return this;
    }

    /// <summary>
    /// 设置水平边框（顶部与底部）。
    /// </summary>
    public BorderBuilder Horizontal(BorderStyle style, ColorSpec? color = null)
    {
        Top(style, color);
        Bottom(style, color);
        return this;
    }

    /// <summary>
    /// 设置垂直边框（左侧与右侧）。
    /// </summary>
    public BorderBuilder Vertical(BorderStyle style, ColorSpec? color = null)
    {
        Left(style, color);
        Right(style, color);
        return this;
    }

    /// <summary>
    /// 清除所有边框。
    /// </summary>
    public BorderBuilder None()
    {
        _top = BorderLine.None;
        _bottom = BorderLine.None;
        _left = BorderLine.None;
        _right = BorderLine.None;
        _diagonal = null;
        return this;
    }

    /// <summary>
    /// 构建 <see cref="BorderSpec"/> 实例。
    /// </summary>
    public BorderSpec Build() => new(_top, _bottom, _left, _right, _diagonal);
}
