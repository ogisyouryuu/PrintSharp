using PrintSharp.Documents;
using PrintSharp.Layout;

namespace PrintSharp.Fluent;

/// <summary>
/// 顶层文档 Fluent 构建器。
/// </summary>
public sealed class DocumentBuilder
{
    private readonly Document _document = new();

    /// <summary>
    /// 初始化 <see cref="DocumentBuilder"/> 类的新实例。
    /// </summary>
    public DocumentBuilder() { }

    /// <summary>
    /// 配置文档元数据信息。
    /// </summary>
    public DocumentBuilder Metadata(Action<MetadataBuilder> configure)
    {
        var builder = new MetadataBuilder();
        configure(builder);
        _document.Metadata = builder.Build();
        return this;
    }

    /// <summary>
    /// 设置文档标题。
    /// </summary>
    public DocumentBuilder Title(string title)
    {
        _document.Metadata.Title = title;
        return this;
    }

    /// <summary>
    /// 设置文档作者。
    /// </summary>
    public DocumentBuilder Author(string author)
    {
        _document.Metadata.Author = author;
        return this;
    }

    /// <summary>
    /// 配置默认工作页面。
    /// </summary>
    /// <param name="configure">页面配置委托。</param>
    public DocumentBuilder Page(Action<PageBuilder> configure)
    {
        var pageBuilder = new PageBuilder(_document.DefaultPage);
        configure(pageBuilder);
        return this;
    }

    /// <summary>
    /// 添加一个新页面并进行配置。
    /// </summary>
    /// <param name="name">页面名称。</param>
    /// <param name="configure">页面配置委托。</param>
    public DocumentBuilder Page(string name, Action<PageBuilder> configure)
    {
        Page page;
        if (_document.Pages.Count == 1 &&
            _document.Pages[0].Cells.Count == 0 &&
            _document.Pages[0].Rows.Count == 0 &&
            _document.Pages[0].Columns.Count == 0 &&
            _document.Pages[0].Name == "Sheet1")
        {
            page = _document.Pages[0];
            page.Name = name;
        }
        else
        {
            page = _document.AddPage(name);
        }

        var pageBuilder = new PageBuilder(page);
        configure(pageBuilder);
        return this;
    }

    #region 默认页面便捷快捷委托

    /// <summary>设置默认页面的默认行高。</summary>
    public DocumentBuilder DefaultRowHeight(float height)
    {
        _document.DefaultPage.Settings = _document.DefaultPage.Settings with { DefaultRowHeight = height };
        return this;
    }

    /// <summary>设置默认页面的默认列宽。</summary>
    public DocumentBuilder DefaultColumnWidth(float width)
    {
        _document.DefaultPage.Settings = _document.DefaultPage.Settings with { DefaultColumnWidth = width };
        return this;
    }

    /// <summary>在默认页面中配置指定列。</summary>
    public DocumentBuilder Column(int index, float width, Action<StyleBuilder>? style = null)
    {
        new PageBuilder(_document.DefaultPage).Column(index, width, style);
        return this;
    }

    /// <summary>在默认页面中连续配置多个列宽。</summary>
    public DocumentBuilder Columns(params float[] widths)
    {
        new PageBuilder(_document.DefaultPage).Columns(widths);
        return this;
    }

    /// <summary>在默认页面中配置指定行。</summary>
    public DocumentBuilder Row(int index, float height, Action<StyleBuilder>? style = null)
    {
        new PageBuilder(_document.DefaultPage).Row(index, height, style);
        return this;
    }

    /// <summary>在默认页面中连续配置多个行高。</summary>
    public DocumentBuilder Rows(params float[] heights)
    {
        new PageBuilder(_document.DefaultPage).Rows(heights);
        return this;
    }

    /// <summary>在默认页面指定坐标配置单元格。</summary>
    public DocumentBuilder Cell(int row, int column, Action<CellBuilder> configure)
    {
        new PageBuilder(_document.DefaultPage).Cell(row, column, configure);
        return this;
    }

    /// <summary>在默认页面指定坐标快速设置单元格值。</summary>
    public DocumentBuilder Cell(int row, int column, object? value)
    {
        new PageBuilder(_document.DefaultPage).Cell(row, column, value);
        return this;
    }

    /// <summary>在默认页面指定坐标与跨度配置单元格。</summary>
    public DocumentBuilder Cell(int row, int column, int rowSpan, int columnSpan, Action<CellBuilder> configure)
    {
        new PageBuilder(_document.DefaultPage).Cell(row, column, rowSpan, columnSpan, configure);
        return this;
    }

    /// <summary>在默认页面使用 Excel A1 格式地址配置单元格或合并区域。</summary>
    public DocumentBuilder Cell(string a1OrRange, Action<CellBuilder> configure)
    {
        new PageBuilder(_document.DefaultPage).Cell(a1OrRange, configure);
        return this;
    }

    /// <summary>在默认页面使用 Excel A1 格式地址快速设置单元格值。</summary>
    public DocumentBuilder Cell(string a1, object? value)
    {
        new PageBuilder(_document.DefaultPage).Cell(a1, value);
        return this;
    }

    /// <summary>在默认页面流式定义行。</summary>
    public DocumentBuilder Row(Action<RowBuilder> configure)
    {
        new PageBuilder(_document.DefaultPage).Row(configure);
        return this;
    }

    /// <summary>在默认页面流式定义指定高度的行。</summary>
    public DocumentBuilder Row(float height, Action<RowBuilder> configure)
    {
        new PageBuilder(_document.DefaultPage).Row(height, configure);
        return this;
    }

    /// <summary>在默认页面流式添加表格。</summary>
    public DocumentBuilder Table(Action<TableBuilder> configure)
    {
        new PageBuilder(_document.DefaultPage).Table(configure);
        return this;
    }

    #endregion

    /// <summary>
    /// 构建并返回最终的 <see cref="Document"/> 实例。
    /// </summary>
    public Document Build() => _document;

    /// <summary>
    /// 构建文档并使用指定（或默认）布局引擎执行物理排版计算。
    /// </summary>
    /// <param name="engine">自定义布局计算引擎，若为 null 则使用全局默认 <see cref="GridLayoutEngine"/>。</param>
    /// <returns>文档的物理排版计算结果。</returns>
    public CalculatedDocumentLayout CalculateLayout(ILayoutEngine? engine = null)
    {
        var layoutEngine = engine ?? GridLayoutEngine.Instance;
        return layoutEngine.CalculateDocument(Build());
    }
}
