using PrintSharp.Styles;

namespace PrintSharp.Fluent;

/// <summary>
/// 单元格样式 Fluent 构建器。
/// </summary>
public sealed class StyleBuilder
{
    private FontSpec? _font;
    private ColorSpec? _foreColor;
    private ColorSpec? _backColor;
    private BorderSpec? _border;
    private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.General;
    private VerticalAlignment _verticalAlignment = VerticalAlignment.Middle;
    private bool _wrapText;
    private PaddingSpec? _padding;
    private string? _format;

    /// <summary>
    /// 使用 <see cref="FontBuilder"/> 配置字体。
    /// </summary>
    public StyleBuilder Font(Action<FontBuilder> configure)
    {
        var builder = new FontBuilder();
        configure(builder);
        _font = builder.Build();
        return this;
    }

    /// <summary>
    /// 直接设置字体参数。
    /// </summary>
    public StyleBuilder Font(string family, float size = 10f, bool bold = false, bool italic = false)
    {
        _font = new FontSpec(family, size, bold, italic);
        return this;
    }

    /// <summary>
    /// 设置字号大小。
    /// </summary>
    public StyleBuilder FontSize(float size)
    {
        _font = (_font ?? FontSpec.Default) with { Size = size };
        return this;
    }

    /// <summary>
    /// 设置字体系列。
    /// </summary>
    public StyleBuilder FontFamily(string family)
    {
        _font = (_font ?? FontSpec.Default) with { Family = family };
        return this;
    }

    /// <summary>
    /// 设置是否加粗。
    /// </summary>
    public StyleBuilder Bold(bool bold = true)
    {
        _font = (_font ?? FontSpec.Default) with { Bold = bold };
        return this;
    }

    /// <summary>
    /// 设置是否斜体。
    /// </summary>
    public StyleBuilder Italic(bool italic = true)
    {
        _font = (_font ?? FontSpec.Default) with { Italic = italic };
        return this;
    }

    /// <summary>
    /// 设置是否有下划线。
    /// </summary>
    public StyleBuilder Underline(bool underline = true)
    {
        _font = (_font ?? FontSpec.Default) with { Underline = underline };
        return this;
    }

    /// <summary>
    /// 设置前景色/文字颜色。
    /// </summary>
    public StyleBuilder Color(ColorSpec color)
    {
        _foreColor = color;
        return this;
    }

    /// <summary>
    /// 使用十六进制字符串设置前景色（如 "#FF0000"）。
    /// </summary>
    public StyleBuilder Color(string hexColor)
    {
        _foreColor = ColorSpec.FromHex(hexColor);
        return this;
    }

    /// <summary>
    /// 设置背景色/填充色。
    /// </summary>
    public StyleBuilder Background(ColorSpec color)
    {
        _backColor = color;
        return this;
    }

    /// <summary>
    /// 使用十六进制字符串设置背景色（如 "#F0F0F0"）。
    /// </summary>
    public StyleBuilder Background(string hexColor)
    {
        _backColor = ColorSpec.FromHex(hexColor);
        return this;
    }

    /// <summary>
    /// 设置水平与垂直对齐方式。
    /// </summary>
    public StyleBuilder Align(HorizontalAlignment horizontal, VerticalAlignment vertical = VerticalAlignment.Middle)
    {
        _horizontalAlignment = horizontal;
        _verticalAlignment = vertical;
        return this;
    }

    /// <summary>
    /// 水平居左。
    /// </summary>
    public StyleBuilder AlignLeft()
    {
        _horizontalAlignment = HorizontalAlignment.Left;
        return this;
    }

    /// <summary>
    /// 水平居中。
    /// </summary>
    public StyleBuilder AlignCenter()
    {
        _horizontalAlignment = HorizontalAlignment.Center;
        return this;
    }

    /// <summary>
    /// 水平居右。
    /// </summary>
    public StyleBuilder AlignRight()
    {
        _horizontalAlignment = HorizontalAlignment.Right;
        return this;
    }

    /// <summary>
    /// 两端对齐。
    /// </summary>
    public StyleBuilder AlignJustify()
    {
        _horizontalAlignment = HorizontalAlignment.Justify;
        return this;
    }

    /// <summary>
    /// 垂直靠顶。
    /// </summary>
    public StyleBuilder AlignTop()
    {
        _verticalAlignment = VerticalAlignment.Top;
        return this;
    }

    /// <summary>
    /// 垂直居中。
    /// </summary>
    public StyleBuilder AlignMiddle()
    {
        _verticalAlignment = VerticalAlignment.Middle;
        return this;
    }

    /// <summary>
    /// 垂直靠底。
    /// </summary>
    public StyleBuilder AlignBottom()
    {
        _verticalAlignment = VerticalAlignment.Bottom;
        return this;
    }

    /// <summary>
    /// 使用 <see cref="BorderBuilder"/> 配置边框。
    /// </summary>
    public StyleBuilder Border(Action<BorderBuilder> configure)
    {
        var builder = new BorderBuilder();
        configure(builder);
        _border = builder.Build();
        return this;
    }

    /// <summary>
    /// 快速为四周设置相同样式边框。
    /// </summary>
    public StyleBuilder BorderAll(BorderStyle style, ColorSpec? color = null)
    {
        _border = BorderSpec.All(style, color);
        return this;
    }

    /// <summary>
    /// 快速为四周设置相同样式边框。
    /// </summary>
    public StyleBuilder BorderAll(BorderStyle style, string hexColor)
    {
        return BorderAll(style, ColorSpec.FromHex(hexColor));
    }

    /// <summary>
    /// 设置统一内边距。
    /// </summary>
    public StyleBuilder Padding(float all)
    {
        _padding = new PaddingSpec(all);
        return this;
    }

    /// <summary>
    /// 设置四周内边距。
    /// </summary>
    public StyleBuilder Padding(float left, float top, float right, float bottom)
    {
        _padding = new PaddingSpec(left, top, right, bottom);
        return this;
    }

    /// <summary>
    /// 设置数值/文本格式化模板（如 "#,##0.00", "yyyy-MM-dd"）。
    /// </summary>
    public StyleBuilder Format(string format)
    {
        _format = format;
        return this;
    }

    /// <summary>
    /// 设置是否自动换行。
    /// </summary>
    public StyleBuilder WrapText(bool wrap = true)
    {
        _wrapText = wrap;
        return this;
    }

    /// <summary>
    /// 构建 <see cref="CellStyle"/> 实例。
    /// </summary>
    public CellStyle Build()
    {
        return new CellStyle
        {
            Font = _font,
            ForeColor = _foreColor,
            BackColor = _backColor,
            Border = _border,
            HorizontalAlignment = _horizontalAlignment,
            VerticalAlignment = _verticalAlignment,
            WrapText = _wrapText,
            Padding = _padding,
            Format = _format
        };
    }
}
