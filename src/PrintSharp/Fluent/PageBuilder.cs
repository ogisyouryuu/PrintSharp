using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Styles;
using PrintSharp.Layout;

namespace PrintSharp.Fluent;

/// <summary>
/// 页面（工作表）Fluent 构建器。
/// </summary>
public sealed class PageBuilder
{
    /// <summary>在空逻辑页面上定义自动分页报表；先设置纸张、余白和列宽。</summary>
    public PageBuilder Report(Action<ReportBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var report = new ReportBuilder(_page);
        configure(report);
        report.Build();
        _currentRowCursor = DocumentPaginator.GetRowCount(_page);
        return this;
    }

    /// <summary>对现有网格启用自动分页，页头与页尾每页重复。</summary>
    public PageBuilder Paginate(int headerRowCount = 0, int footerRowCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(headerRowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(footerRowCount);
        _page.Pagination = new PaginationSettings { HeaderRowCount = headerRowCount, FooterRowCount = footerRowCount };
        return this;
    }
    private readonly Page _page;
    private int _currentRowCursor;

    /// <summary>
    /// 初始化 <see cref="PageBuilder"/> 类的新实例。
    /// </summary>
    /// <param name="page">待配置的 <see cref="Page"/> 实例。</param>
    public PageBuilder(Page page)
    {
        ArgumentNullException.ThrowIfNull(page);
        _page = page;
    }

    /// <summary>
    /// 获取当前正在构建的底层 <see cref="Page"/> 实例。
    /// </summary>
    public Page Page => _page;

    /// <summary>
    /// 设置页面名称（对应 Excel 工作表名）。
    /// </summary>
    public PageBuilder Name(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _page.Name = name;
        return this;
    }

    /// <summary>
    /// 设置页面页码。
    /// </summary>
    public PageBuilder PageNumber(int pageNumber)
    {
        _page.PageNumber = pageNumber;
        return this;
    }

    /// <summary>
    /// 配置页面设置（如边距、纸张尺寸、方向等）。
    /// </summary>
    public PageBuilder Settings(Action<PageSettingsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new PageSettingsBuilder();
        configure(builder);
        _page.Settings = builder.Build();
        return this;
    }

    /// <summary>
    /// 设置当前页面未显式定义高度行的默认行高。
    /// </summary>
    public PageBuilder DefaultRowHeight(float height)
    {
        _page.Settings = _page.Settings with { DefaultRowHeight = height };
        return this;
    }

    /// <summary>
    /// 设置当前页面未显式定义宽度列的默认列宽。
    /// </summary>
    public PageBuilder DefaultColumnWidth(float width)
    {
        _page.Settings = _page.Settings with { DefaultColumnWidth = width };
        return this;
    }

    /// <summary>
    /// 定义指定列的逻辑宽度与默认样式。
    /// </summary>
    /// <param name="columnIndex">0 基列索引。</param>
    /// <param name="width">逻辑列宽。</param>
    /// <param name="style">列的默认样式配置委托（可选）。</param>
    public PageBuilder Column(int columnIndex, float width, Action<StyleBuilder>? style = null)
    {
        var col = _page.EnsureColumn(columnIndex, width);
        col.Width = width;
        if (style is not null)
        {
            var sb = new StyleBuilder();
            style(sb);
            col.DefaultStyle = sb.Build();
        }
        return this;
    }

    /// <summary>
    /// 连续定义从第 0 列开始的多个列宽。
    /// </summary>
    /// <param name="widths">各列的逻辑宽度。</param>
    public PageBuilder Columns(params float[] widths)
    {
        for (int i = 0; i < widths.Length; i++)
        {
            _page.EnsureColumn(i, widths[i]).Width = widths[i];
        }
        return this;
    }

    /// <summary>
    /// 定义指定行的逻辑高度与默认样式。
    /// </summary>
    /// <param name="rowIndex">0 基行索引。</param>
    /// <param name="height">逻辑行高。</param>
    /// <param name="style">行的默认样式配置委托（可选）。</param>
    public PageBuilder Row(int rowIndex, float height, Action<StyleBuilder>? style = null)
    {
        var row = _page.EnsureRow(rowIndex, height);
        row.Height = height;
        if (style is not null)
        {
            var sb = new StyleBuilder();
            style(sb);
            row.DefaultStyle = sb.Build();
        }
        return this;
    }

    /// <summary>
    /// 连续定义从第 0 行开始的多个行高。
    /// </summary>
    /// <param name="heights">各行的逻辑高度。</param>
    public PageBuilder Rows(params float[] heights)
    {
        for (int i = 0; i < heights.Length; i++)
        {
            _page.EnsureRow(i, heights[i]).Height = heights[i];
        }
        return this;
    }

    /// <summary>
    /// 在指定网格坐标（行、列）配置单元格。
    /// </summary>
    /// <param name="row">0 基行索引。</param>
    /// <param name="column">0 基列索引。</param>
    /// <param name="configure">单元格配置委托。</param>
    public PageBuilder Cell(int row, int column, Action<CellBuilder> configure)
    {
        var builder = new CellBuilder(row, column);
        configure(builder);
        _page.SetCell(builder.Build());
        return this;
    }

    /// <summary>
    /// 在指定网格坐标快速添加单元格值。
    /// </summary>
    public PageBuilder Cell(int row, int column, object? value)
    {
        return Cell(row, column, c => c.Value(value));
    }

    /// <summary>
    /// 在指定网格坐标并指定合并跨度配置单元格。
    /// </summary>
    /// <param name="row">0 基行索引。</param>
    /// <param name="column">0 基列索引。</param>
    /// <param name="rowSpan">跨行数。</param>
    /// <param name="columnSpan">跨列数。</param>
    /// <param name="configure">单元格配置委托。</param>
    public PageBuilder Cell(int row, int column, int rowSpan, int columnSpan, Action<CellBuilder> configure)
    {
        var builder = new CellBuilder(row, column, rowSpan, columnSpan);
        configure(builder);
        _page.SetCell(builder.Build());
        return this;
    }

    /// <summary>
    /// 使用 Excel A1 格式地址（如 "A1" 或 "B2:D4"）配置单元格或合并区域。
    /// </summary>
    /// <param name="a1OrRange">Excel 地址或区域字符串。</param>
    /// <param name="configure">单元格配置委托。</param>
    public PageBuilder Cell(string a1OrRange, Action<CellBuilder> configure)
    {
        var (startRow, startCol, rowSpan, colSpan) = CellReference.ParseRange(a1OrRange);
        return Cell(startRow, startCol, rowSpan, colSpan, configure);
    }

    /// <summary>
    /// 使用 Excel A1 格式地址快速添加单元格值。
    /// </summary>
    public PageBuilder Cell(string a1, object? value)
    {
        return Cell(a1, c => c.Value(value));
    }

    /// <summary>
    /// 在当前游标行按序流式定义行内单元格，行高使用页面默认行高，游标自动下移。
    /// </summary>
    /// <param name="configure">行构建委托。</param>
    public PageBuilder Row(Action<RowBuilder> configure)
    {
        return Row(_currentRowCursor, _page.Settings.DefaultRowHeight, configure);
    }

    /// <summary>
    /// 在当前游标行按序流式定义行内单元格并指定行高，游标自动下移。
    /// </summary>
    /// <param name="height">行高。</param>
    /// <param name="configure">行构建委托。</param>
    public PageBuilder Row(float height, Action<RowBuilder> configure)
    {
        return Row(_currentRowCursor, height, configure);
    }

    /// <summary>
    /// 在指定行按序流式定义行内单元格。
    /// </summary>
    /// <param name="rowIndex">0 基行索引。</param>
    /// <param name="height">行高。</param>
    /// <param name="configure">行构建委托。</param>
    public PageBuilder Row(int rowIndex, float height, Action<RowBuilder> configure)
    {
        var rowDef = _page.EnsureRow(rowIndex, height);
        var rowBuilder = new RowBuilder(rowIndex);
        configure(rowBuilder);

        var rowStyle = rowBuilder.BuildRowStyle();
        if (rowStyle is not null)
        {
            rowDef.DefaultStyle = (rowDef.DefaultStyle ?? CellStyle.Default).MergeWith(rowStyle);
        }

        foreach (var cell in rowBuilder.BuildCells())
        {
            _page.SetCell(cell);
        }

        _currentRowCursor = Math.Max(_currentRowCursor, rowIndex + 1);
        return this;
    }

    /// <summary>
    /// 在当前游标行处使用 <see cref="TableBuilder"/> 声明式添加表格。
    /// </summary>
    /// <param name="configure">表格构建委托。</param>
    public PageBuilder Table(Action<TableBuilder> configure)
    {
        return Table(_currentRowCursor, 0, configure);
    }

    /// <summary>
    /// 在指定网格起始位置使用 <see cref="TableBuilder"/> 声明式添加表格。
    /// </summary>
    /// <param name="startRow">起始 0 基行索引。</param>
    /// <param name="startColumn">起始 0 基列索引。</param>
    /// <param name="configure">表格构建委托。</param>
    public PageBuilder Table(int startRow, int startColumn, Action<TableBuilder> configure)
    {
        var tableBuilder = new TableBuilder(_page, startRow, startColumn);
        configure(tableBuilder);
        _currentRowCursor = Math.Max(_currentRowCursor, tableBuilder.NextRowIndex);
        return this;
    }
}
