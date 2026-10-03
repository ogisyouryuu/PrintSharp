using PrintSharp.Cells;
using PrintSharp.Styles;

namespace PrintSharp.Fluent;

/// <summary>
/// 单元格 Fluent 构建器。
/// </summary>
public sealed class CellBuilder
{
    private int _row;
    private int _column;
    private int _rowSpan = 1;
    private int _columnSpan = 1;
    private object? _value;
    private CellType _type = CellType.Text;
    private StyleBuilder? _styleBuilder;
    private CellStyle? _directStyle;

    /// <summary>
    /// 初始化 <see cref="CellBuilder"/> 类的新实例。
    /// </summary>
    /// <param name="row">0 基行索引。</param>
    /// <param name="column">0 基列索引。</param>
    /// <param name="rowSpan">跨行数。</param>
    /// <param name="columnSpan">跨列数。</param>
    public CellBuilder(int row, int column, int rowSpan = 1, int columnSpan = 1)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        _row = row;
        _column = column;
        _rowSpan = Math.Max(1, rowSpan);
        _columnSpan = Math.Max(1, columnSpan);
    }

    /// <summary>
    /// 获取当前单元格的 0 基行索引。
    /// </summary>
    public int RowIndex => _row;

    /// <summary>
    /// 获取当前单元格的 0 基列索引。
    /// </summary>
    public int ColumnIndex => _column;

    /// <summary>
    /// 获取当前单元格的跨行数。
    /// </summary>
    public int RowSpanCount => _rowSpan;

    /// <summary>
    /// 获取当前单元格的跨列数。
    /// </summary>
    public int ColumnSpanCount => _columnSpan;

    /// <summary>
    /// 设置单元格的值。
    /// </summary>
    public CellBuilder Value(object? value)
    {
        _value = value;
        return this;
    }

    /// <summary>
    /// 设置文本值。
    /// </summary>
    public CellBuilder Text(string? text)
    {
        _value = text;
        _type = CellType.Text;
        return this;
    }

    /// <summary>
    /// 设置图像数据值。
    /// </summary>
    public CellBuilder Image(byte[] imageBytes)
    {
        _value = imageBytes;
        _type = CellType.Image;
        return this;
    }

    /// <summary>
    /// 设置公式字符串（如 "=SUM(A1:A10)"）。
    /// </summary>
    public CellBuilder Formula(string formula)
    {
        _value = formula;
        _type = CellType.Formula;
        return this;
    }

    /// <summary>
    /// 设置内容类型。
    /// </summary>
    public CellBuilder Type(CellType type)
    {
        _type = type;
        return this;
    }

    /// <summary>
    /// 设置跨行跨列合并范围。
    /// </summary>
    public CellBuilder Span(int rowSpan, int columnSpan)
    {
        if (rowSpan < 1) throw new ArgumentOutOfRangeException(nameof(rowSpan));
        if (columnSpan < 1) throw new ArgumentOutOfRangeException(nameof(columnSpan));
        _rowSpan = rowSpan;
        _columnSpan = columnSpan;
        return this;
    }

    /// <summary>
    /// 设置跨行数。
    /// </summary>
    public CellBuilder RowSpan(int rowSpan)
    {
        if (rowSpan < 1) throw new ArgumentOutOfRangeException(nameof(rowSpan));
        _rowSpan = rowSpan;
        return this;
    }

    /// <summary>
    /// 设置跨列数。
    /// </summary>
    public CellBuilder ColumnSpan(int columnSpan)
    {
        if (columnSpan < 1) throw new ArgumentOutOfRangeException(nameof(columnSpan));
        _columnSpan = columnSpan;
        return this;
    }

    /// <summary>
    /// 使用 <see cref="StyleBuilder"/> 配置单元格样式。
    /// </summary>
    public CellBuilder Style(Action<StyleBuilder> configure)
    {
        _styleBuilder ??= new StyleBuilder();
        configure(_styleBuilder);
        return this;
    }

    /// <summary>
    /// 直接设置单元格样式实例。
    /// </summary>
    public CellBuilder Style(CellStyle style)
    {
        _directStyle = style;
        return this;
    }

    private StyleBuilder EnsureStyleBuilder() => _styleBuilder ??= new StyleBuilder();

    #region 便捷样式快捷方法

    /// <summary>设置字体。</summary>
    public CellBuilder Font(string family, float size = 10f, bool bold = false, bool italic = false)
    {
        EnsureStyleBuilder().Font(family, size, bold, italic);
        return this;
    }

    /// <summary>设置字号。</summary>
    public CellBuilder FontSize(float size)
    {
        EnsureStyleBuilder().FontSize(size);
        return this;
    }

    /// <summary>设置字体系列。</summary>
    public CellBuilder FontFamily(string family)
    {
        EnsureStyleBuilder().FontFamily(family);
        return this;
    }

    /// <summary>设置是否加粗。</summary>
    public CellBuilder Bold(bool bold = true)
    {
        EnsureStyleBuilder().Bold(bold);
        return this;
    }

    /// <summary>设置是否斜体。</summary>
    public CellBuilder Italic(bool italic = true)
    {
        EnsureStyleBuilder().Italic(italic);
        return this;
    }

    /// <summary>设置是否有下划线。</summary>
    public CellBuilder Underline(bool underline = true)
    {
        EnsureStyleBuilder().Underline(underline);
        return this;
    }

    /// <summary>设置文字颜色。</summary>
    public CellBuilder Color(ColorSpec color)
    {
        EnsureStyleBuilder().Color(color);
        return this;
    }

    /// <summary>设置文字颜色（十六进制字符串）。</summary>
    public CellBuilder Color(string hexColor)
    {
        EnsureStyleBuilder().Color(hexColor);
        return this;
    }

    /// <summary>设置背景颜色。</summary>
    public CellBuilder Background(ColorSpec color)
    {
        EnsureStyleBuilder().Background(color);
        return this;
    }

    /// <summary>设置背景颜色（十六进制字符串）。</summary>
    public CellBuilder Background(string hexColor)
    {
        EnsureStyleBuilder().Background(hexColor);
        return this;
    }

    /// <summary>设置水平和垂直对齐方式。</summary>
    public CellBuilder Align(HorizontalAlignment horizontal, VerticalAlignment vertical = VerticalAlignment.Middle)
    {
        EnsureStyleBuilder().Align(horizontal, vertical);
        return this;
    }

    /// <summary>水平居左。</summary>
    public CellBuilder AlignLeft()
    {
        EnsureStyleBuilder().AlignLeft();
        return this;
    }

    /// <summary>水平居中。</summary>
    public CellBuilder AlignCenter()
    {
        EnsureStyleBuilder().AlignCenter();
        return this;
    }

    /// <summary>水平居右。</summary>
    public CellBuilder AlignRight()
    {
        EnsureStyleBuilder().AlignRight();
        return this;
    }

    /// <summary>水平两端对齐。</summary>
    public CellBuilder AlignJustify()
    {
        EnsureStyleBuilder().AlignJustify();
        return this;
    }

    /// <summary>垂直靠顶。</summary>
    public CellBuilder AlignTop()
    {
        EnsureStyleBuilder().AlignTop();
        return this;
    }

    /// <summary>垂直居中。</summary>
    public CellBuilder AlignMiddle()
    {
        EnsureStyleBuilder().AlignMiddle();
        return this;
    }

    /// <summary>垂直靠底。</summary>
    public CellBuilder AlignBottom()
    {
        EnsureStyleBuilder().AlignBottom();
        return this;
    }

    /// <summary>配置边框。</summary>
    public CellBuilder Border(Action<BorderBuilder> configure)
    {
        EnsureStyleBuilder().Border(configure);
        return this;
    }

    /// <summary>设置四周统一边框。</summary>
    public CellBuilder BorderAll(BorderStyle style, ColorSpec? color = null)
    {
        EnsureStyleBuilder().BorderAll(style, color);
        return this;
    }

    /// <summary>设置四周统一边框（十六进制颜色）。</summary>
    public CellBuilder BorderAll(BorderStyle style, string hexColor)
    {
        EnsureStyleBuilder().BorderAll(style, hexColor);
        return this;
    }

    /// <summary>设置下边框。</summary>
    public CellBuilder BorderBottom(BorderStyle style, ColorSpec? color = null)
    {
        EnsureStyleBuilder().Border(b => b.Bottom(style, color));
        return this;
    }

    /// <summary>设置内边距。</summary>
    public CellBuilder Padding(float all)
    {
        EnsureStyleBuilder().Padding(all);
        return this;
    }

    /// <summary>设置内边距。</summary>
    public CellBuilder Padding(float left, float top, float right, float bottom)
    {
        EnsureStyleBuilder().Padding(left, top, right, bottom);
        return this;
    }

    /// <summary>设置格式化字符串。</summary>
    public CellBuilder Format(string format)
    {
        EnsureStyleBuilder().Format(format);
        return this;
    }

    /// <summary>设置自动换行。</summary>
    public CellBuilder WrapText(bool wrap = true)
    {
        EnsureStyleBuilder().WrapText(wrap);
        return this;
    }

    #endregion

    /// <summary>
    /// 构建 <see cref="Cell"/> 实例。
    /// </summary>
    public Cell Build()
    {
        CellStyle? style = _directStyle;
        if (_styleBuilder is not null)
        {
            var builtStyle = _styleBuilder.Build();
            style = style is not null ? style.MergeWith(builtStyle) : builtStyle;
        }

        return new Cell(
            _row,
            _column,
            _value,
            _rowSpan,
            _columnSpan,
            _type,
            style);
    }
}
