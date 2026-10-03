namespace PrintSharp.Styles;

/// <summary>
/// 表示单元格内部的逻辑内边距（Padding）。
/// </summary>
public readonly record struct PaddingSpec
{
    /// <summary>
    /// 获取左侧内边距。
    /// </summary>
    public float Left { get; init; }

    /// <summary>
    /// 获取顶部内边距。
    /// </summary>
    public float Top { get; init; }

    /// <summary>
    /// 获取右侧内边距。
    /// </summary>
    public float Right { get; init; }

    /// <summary>
    /// 获取底部内边距。
    /// </summary>
    public float Bottom { get; init; }

    /// <summary>
    /// 获取水平内边距总和（Left + Right）。
    /// </summary>
    public float Horizontal => Left + Right;

    /// <summary>
    /// 获取垂直内边距总和（Top + Bottom）。
    /// </summary>
    public float Vertical => Top + Bottom;

    /// <summary>
    /// 初始化 <see cref="PaddingSpec"/> 结构体的新实例。
    /// </summary>
    public PaddingSpec(float left, float top, float right, float bottom)
    {
        Left = Math.Max(0, left);
        Top = Math.Max(0, top);
        Right = Math.Max(0, right);
        Bottom = Math.Max(0, bottom);
    }

    /// <summary>
    /// 使用指定的统一边距初始化。
    /// </summary>
    public PaddingSpec(float all) : this(all, all, all, all) { }

    /// <summary>
    /// 使用指定的水平与垂直边距初始化。
    /// </summary>
    public PaddingSpec(float horizontal, float vertical) : this(horizontal, vertical, horizontal, vertical) { }

    /// <summary>
    /// 获取零内边距。
    /// </summary>
    public static PaddingSpec Zero => new(0f);
}
