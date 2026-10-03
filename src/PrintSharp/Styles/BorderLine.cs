namespace PrintSharp.Styles;

/// <summary>
/// 表示单元格单侧边框线的规格（样式、颜色、逻辑粗细）。
/// </summary>
public sealed record BorderLine
{
    /// <summary>
    /// 获取边框线样式。
    /// </summary>
    public BorderStyle Style { get; init; } = BorderStyle.None;

    /// <summary>
    /// 获取边框线颜色，若为 null 则默认为黑色。
    /// </summary>
    public ColorSpec? Color { get; init; }

    /// <summary>
    /// 获取边框线的逻辑粗细（单位：逻辑排版单位；若小于等于 0 则由渲染器根据 <see cref="Style"/> 决定）。
    /// </summary>
    public float Width { get; init; }

    /// <summary>
    /// 初始化 <see cref="BorderLine"/> 类的新实例。
    /// </summary>
    public BorderLine() { }

    /// <summary>
    /// 初始化 <see cref="BorderLine"/> 类的新实例。
    /// </summary>
    /// <param name="style">边框样式。</param>
    /// <param name="color">边框颜色。</param>
    /// <param name="width">自定义粗细（可选）。</param>
    public BorderLine(BorderStyle style, ColorSpec? color = null, float width = 0f)
    {
        Style = style;
        Color = color ?? ColorSpec.Black;
        Width = width;
    }

    /// <summary>
    /// 获取一个表示无边框的实例。
    /// </summary>
    public static BorderLine None => new(BorderStyle.None);

    /// <summary>
    /// 创建细边框线。
    /// </summary>
    public static BorderLine Thin(ColorSpec? color = null) => new(BorderStyle.Thin, color);

    /// <summary>
    /// 创建中等粗度边框线。
    /// </summary>
    public static BorderLine Medium(ColorSpec? color = null) => new(BorderStyle.Medium, color);

    /// <summary>
    /// 创建粗边框线。
    /// </summary>
    public static BorderLine Thick(ColorSpec? color = null) => new(BorderStyle.Thick, color);

    /// <summary>
    /// 创建双边框线。
    /// </summary>
    public static BorderLine Double(ColorSpec? color = null) => new(BorderStyle.Double, color);

    /// <summary>
    /// 创建虚线边框线。
    /// </summary>
    public static BorderLine Dashed(ColorSpec? color = null) => new(BorderStyle.Dashed, color);

    /// <summary>
    /// 创建点线边框线。
    /// </summary>
    public static BorderLine Dotted(ColorSpec? color = null) => new(BorderStyle.Dotted, color);
}
