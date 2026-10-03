using PrintSharp.Documents;
using PrintSharp.Styles;

namespace PrintSharp.Fluent;

/// <summary>
/// 用于在网格页面中声明式构建表格数据的 Fluent 构建器。
/// </summary>
public sealed class TableBuilder
{
    private readonly Page _page;
    private int _startRow;
    private int _startColumn;
    private int _currentRow;
    private StyleBuilder? _headerStyleBuilder;
    private StyleBuilder? _rowStyleBuilder;
    private float _defaultRowHeight = 22f;
    private float _defaultHeaderHeight = 26f;

    /// <summary>
    /// 初始化 <see cref="TableBuilder"/> 类的新实例。
    /// </summary>
    /// <param name="page">所属页面。</param>
    /// <param name="startRow">表格起始 0 基行索引。</param>
    /// <param name="startColumn">表格起始 0 基列索引。</param>
    public TableBuilder(Page page, int startRow = 0, int startColumn = 0)
    {
        _page = page;
        _startRow = startRow;
        _startColumn = startColumn;
        _currentRow = startRow;
    }

    /// <summary>
    /// 设置表格在网格中的起始位置。
    /// </summary>
    /// <param name="row">0 基起始行索引。</param>
    /// <param name="column">0 基起始列索引。</param>
    public TableBuilder StartAt(int row, int column = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        _startRow = row;
        _startColumn = column;
        _currentRow = row;
        return this;
    }

    /// <summary>
    /// 连续定义从起始列开始的多个列宽。
    /// </summary>
    /// <param name="widths">列宽数组。</param>
    public TableBuilder Columns(params float[] widths)
    {
        for (int i = 0; i < widths.Length; i++)
        {
            _page.EnsureColumn(_startColumn + i, widths[i]);
        }
        return this;
    }

    /// <summary>
    /// 设置表头行默认高度。
    /// </summary>
    public TableBuilder HeaderHeight(float height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        _defaultHeaderHeight = height;
        return this;
    }

    /// <summary>
    /// 设置数据行默认高度。
    /// </summary>
    public TableBuilder RowHeight(float height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        _defaultRowHeight = height;
        return this;
    }

    /// <summary>
    /// 设置表头的全局默认样式。
    /// </summary>
    public TableBuilder DefaultHeaderStyle(Action<StyleBuilder> configure)
    {
        _headerStyleBuilder ??= new StyleBuilder();
        configure(_headerStyleBuilder);
        return this;
    }

    /// <summary>
    /// 设置数据行的全局默认样式。
    /// </summary>
    public TableBuilder DefaultRowStyle(Action<StyleBuilder> configure)
    {
        _rowStyleBuilder ??= new StyleBuilder();
        configure(_rowStyleBuilder);
        return this;
    }

    /// <summary>
    /// 添加一个表头行。
    /// </summary>
    public TableBuilder Header(Action<RowBuilder> configure)
    {
        return Header(_defaultHeaderHeight, configure);
    }

    /// <summary>
    /// 添加指定高度的表头行。
    /// </summary>
    public TableBuilder Header(float height, Action<RowBuilder> configure)
    {
        var rowDef = _page.EnsureRow(_currentRow, height);
        if (_headerStyleBuilder is not null)
        {
            rowDef.DefaultStyle = (rowDef.DefaultStyle ?? CellStyle.Default).MergeWith(_headerStyleBuilder.Build());
        }

        var rowBuilder = new RowBuilder(_currentRow, _startColumn);
        configure(rowBuilder);

        foreach (var cell in rowBuilder.BuildCells())
        {
            _page.SetCell(cell);
        }

        _currentRow++;
        return this;
    }

    /// <summary>
    /// 添加一个数据行。
    /// </summary>
    public TableBuilder Row(Action<RowBuilder> configure)
    {
        return Row(_defaultRowHeight, configure);
    }

    /// <summary>
    /// 添加指定高度的数据行。
    /// </summary>
    public TableBuilder Row(float height, Action<RowBuilder> configure)
    {
        var rowDef = _page.EnsureRow(_currentRow, height);
        if (_rowStyleBuilder is not null)
        {
            rowDef.DefaultStyle = (rowDef.DefaultStyle ?? CellStyle.Default).MergeWith(_rowStyleBuilder.Build());
        }

        var rowBuilder = new RowBuilder(_currentRow, _startColumn);
        configure(rowBuilder);

        foreach (var cell in rowBuilder.BuildCells())
        {
            _page.SetCell(cell);
        }

        _currentRow++;
        return this;
    }

    /// <summary>
    /// 绑定数据源集合，并为集合中每个元素生成数据行。
    /// </summary>
    /// <typeparam name="T">数据项类型。</typeparam>
    /// <param name="items">数据源集合。</param>
    /// <param name="configureRow">行配置委托。</param>
    public TableBuilder Rows<T>(IEnumerable<T> items, Action<RowBuilder, T> configureRow)
    {
        return Rows(items, _defaultRowHeight, configureRow);
    }

    /// <summary>
    /// 绑定数据源集合，并为集合中每个元素生成指定高度的数据行。
    /// </summary>
    /// <typeparam name="T">数据项类型。</typeparam>
    /// <param name="items">数据源集合。</param>
    /// <param name="rowHeight">数据行高度。</param>
    /// <param name="configureRow">行配置委托。</param>
    public TableBuilder Rows<T>(IEnumerable<T> items, float rowHeight, Action<RowBuilder, T> configureRow)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(configureRow);

        foreach (var item in items)
        {
            Row(rowHeight, r => configureRow(r, item));
        }

        return this;
    }

    /// <summary>
    /// 获取当前表格输出结束后的下一行索引。
    /// </summary>
    public int NextRowIndex => _currentRow;
}
