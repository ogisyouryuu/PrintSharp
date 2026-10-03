namespace PrintSharp.Styles;

/// <summary>
/// 表示与特定渲染引擎解耦的单元格样式规范。
/// </summary>
public sealed record CellStyle
{
    /// <summary>
    /// 获取逻辑字体规范。
    /// </summary>
    public FontSpec? Font { get; init; }

    /// <summary>
    /// 获取前景色/文字颜色。
    /// </summary>
    public ColorSpec? ForeColor { get; init; }

    /// <summary>
    /// 获取背景色/填充颜色。
    /// </summary>
    public ColorSpec? BackColor { get; init; }

    /// <summary>
    /// 获取边框规范。
    /// </summary>
    public BorderSpec? Border { get; init; }

    /// <summary>
    /// 获取水平对齐方式。
    /// </summary>
    public HorizontalAlignment HorizontalAlignment { get; init; } = HorizontalAlignment.General;

    /// <summary>
    /// 获取垂直对齐方式。
    /// </summary>
    public VerticalAlignment VerticalAlignment { get; init; } = VerticalAlignment.Middle;

    /// <summary>
    /// 获取一个值，该值指示当文本超出单元格宽度时是否自动换行。
    /// </summary>
    public bool WrapText { get; init; }

    /// <summary>
    /// 获取单元格内边距规范。
    /// </summary>
    public PaddingSpec? Padding { get; init; }

    /// <summary>
    /// 获取格式化字符串（例如数字格式 "#,##0.00"、日期格式 "yyyy-MM-dd" 等）。
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// 获取默认样式实例。
    /// </summary>
    public static CellStyle Default => new();

    /// <summary>
    /// 将当前样式与覆盖样式合并，返回生成的新样式。
    /// </summary>
    /// <param name="overlay">覆盖在当前样式之上的样式。</param>
    /// <returns>合并后的新 <see cref="CellStyle"/> 实例。</returns>
    public CellStyle MergeWith(CellStyle? overlay)
    {
        if (overlay is null) return this;

        return this with
        {
            Font = overlay.Font ?? Font,
            ForeColor = overlay.ForeColor ?? ForeColor,
            BackColor = overlay.BackColor ?? BackColor,
            Border = overlay.Border ?? Border,
            HorizontalAlignment = overlay.HorizontalAlignment != HorizontalAlignment.General
                ? overlay.HorizontalAlignment
                : HorizontalAlignment,
            VerticalAlignment = overlay.VerticalAlignment != VerticalAlignment.Middle
                ? overlay.VerticalAlignment
                : VerticalAlignment,
            WrapText = overlay.WrapText || WrapText,
            Padding = overlay.Padding ?? Padding,
            Format = overlay.Format ?? Format
        };
    }
}
