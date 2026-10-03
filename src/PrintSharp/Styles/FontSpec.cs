namespace PrintSharp.Styles;

/// <summary>
/// 表示与特定渲染器和操作系统解耦的逻辑字体规范。
/// </summary>
public sealed record FontSpec
{
    /// <summary>
    /// 获取字体系列名称（逻辑名称，例如 "Yu Gothic"、"Arial" 等；若为 null，则由解析器选择默认字体）。
    /// </summary>
    public string? Family { get; init; }

    /// <summary>
    /// 获取逻辑字号大小（排版点数，默认为 10）。
    /// </summary>
    public float Size { get; init; } = 10f;

    /// <summary>
    /// 获取一个值，该值指示字体是否为粗体。
    /// </summary>
    public bool Bold { get; init; }

    /// <summary>
    /// 获取一个值，该值指示字体是否为斜体。
    /// </summary>
    public bool Italic { get; init; }

    /// <summary>
    /// 获取一个值，该值指示字体是否带有下划线。
    /// </summary>
    public bool Underline { get; init; }

    /// <summary>
    /// 获取一个值，该值指示字体是否带有删除线。
    /// </summary>
    public bool StrikeThrough { get; init; }

    /// <summary>
    /// 初始化 <see cref="FontSpec"/> 类的新实例。
    /// </summary>
    public FontSpec() { }

    /// <summary>
    /// 初始化 <see cref="FontSpec"/> 类的新实例。
    /// </summary>
    /// <param name="family">字体名称。</param>
    /// <param name="size">逻辑字号大小。</param>
    /// <param name="bold">是否粗体。</param>
    /// <param name="italic">是否斜体。</param>
    /// <param name="underline">是否下划线。</param>
    /// <param name="strikeThrough">是否删除线。</param>
    public FontSpec(
        string? family = null,
        float size = 10f,
        bool bold = false,
        bool italic = false,
        bool underline = false,
        bool strikeThrough = false)
    {
        Family = family;
        Size = size > 0 ? size : 10f;
        Bold = bold;
        Italic = italic;
        Underline = underline;
        StrikeThrough = strikeThrough;
    }

    /// <summary>
    /// 获取默认的常规字体规范（大小 10pt）。
    /// </summary>
    public static FontSpec Default => new();
}
