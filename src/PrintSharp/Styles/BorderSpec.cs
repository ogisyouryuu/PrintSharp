namespace PrintSharp.Styles;

/// <summary>
/// 表示单元格四周边框及对角线边框的完整规范。
/// </summary>
public sealed record BorderSpec
{
    /// <summary>
    /// 获取上边框。
    /// </summary>
    public BorderLine? Top { get; init; }

    /// <summary>
    /// 获取下边框。
    /// </summary>
    public BorderLine? Bottom { get; init; }

    /// <summary>
    /// 获取左边框。
    /// </summary>
    public BorderLine? Left { get; init; }

    /// <summary>
    /// 获取右边框。
    /// </summary>
    public BorderLine? Right { get; init; }

    /// <summary>
    /// 获取对角线边框（可选）。
    /// </summary>
    public BorderLine? Diagonal { get; init; }

    /// <summary>
    /// 初始化 <see cref="BorderSpec"/> 类的新实例。
    /// </summary>
    public BorderSpec() { }

    /// <summary>
    /// 初始化 <see cref="BorderSpec"/> 类的新实例。
    /// </summary>
    public BorderSpec(
        BorderLine? top = null,
        BorderLine? bottom = null,
        BorderLine? left = null,
        BorderLine? right = null,
        BorderLine? diagonal = null)
    {
        Top = top;
        Bottom = bottom;
        Left = left;
        Right = right;
        Diagonal = diagonal;
    }

    /// <summary>
    /// 创建四边均相同的边框规范。
    /// </summary>
    public static BorderSpec All(BorderLine border) => new(border, border, border, border);

    /// <summary>
    /// 创建四边均为指定样式的边框规范。
    /// </summary>
    public static BorderSpec All(BorderStyle style, ColorSpec? color = null)
    {
        var line = new BorderLine(style, color);
        return new BorderSpec(line, line, line, line);
    }

    /// <summary>
    /// 创建包含指定上下左右样式的边框。
    /// </summary>
    public static BorderSpec Box(BorderLine horizontal, BorderLine vertical) =>
        new(horizontal, horizontal, vertical, vertical);
}
