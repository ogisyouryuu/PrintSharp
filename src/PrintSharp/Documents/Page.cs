using PrintSharp.Cells;
using PrintSharp.Grid;

namespace PrintSharp.Documents;

/// <summary>
/// 表示网格文档中的一个页面或工作表（Sheet）。
/// 包含行（Rows）、列（Columns）以及单元格（Cells）。
/// </summary>
public sealed class Page
{
    private readonly List<Row> _rows = [];
    private readonly List<Column> _columns = [];
    private readonly List<Cell> _cells = [];

    /// <summary>
    /// 获取或设置页码（从 1 开始）。
    /// </summary>
    public int PageNumber { get; set; } = 1;

    /// <summary>获取或设置当前页码，与 PageNumber 同步。</summary>
    public int CurrentPageNumber
    {
        get => PageNumber;
        set => PageNumber = value;
    }

    /// <summary>
    /// 获取或设置页面名称（对应 Excel 的工作表名，例如 "Sheet1"）。
    /// </summary>
    public string Name { get; set; } = "Sheet1";

    /// <summary>
    /// 获取该页面的页面设置（如边距、方向、默认行高列宽等）。
    /// </summary>
    public PageSettings Settings { get; set; } = PageSettings.Default;

    /// <summary>
    /// 获取该页面已显式定义的行集合。
    /// </summary>
    public IList<Row> Rows => _rows;

    /// <summary>
    /// 获取该页面已显式定义的列集合。
    /// </summary>
    public IList<Column> Columns => _columns;

    /// <summary>
    /// 获取该页面包含的所有单元格集合。
    /// </summary>
    public IList<Cell> Cells => _cells;

    /// <summary>
    /// 初始化 <see cref="Page"/> 类的新实例。
    /// </summary>
    public Page() { }

    /// <summary>
    /// 初始化 <see cref="Page"/> 类的新实例。
    /// </summary>
    /// <param name="name">页面名称。</param>
    /// <param name="pageNumber">页码。</param>
    public Page(string name, int pageNumber = 1)
    {
        Name = name;
        PageNumber = pageNumber;
    }

    /// <summary>
    /// 获取指定索引的行定义，若不存在则创建并添加到集合中。
    /// </summary>
    /// <param name="rowIndex">0 基行索引。</param>
    /// <param name="height">若创建新行时的默认高度，为 null 则使用页面默认行高。</param>
    /// <returns>对应的 <see cref="Row"/> 实例。</returns>
    public Row EnsureRow(int rowIndex, float? height = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);

        var existing = _rows.FirstOrDefault(r => r.Index == rowIndex);
        if (existing is not null) return existing;

        var newRow = new Row(rowIndex, height ?? Settings.DefaultRowHeight);
        _rows.Add(newRow);
        return newRow;
    }

    /// <summary>
    /// 获取指定索引的列定义，若不存在则创建并添加到集合中。
    /// </summary>
    /// <param name="columnIndex">0 基列索引。</param>
    /// <param name="width">若创建新列时的默认宽度，为 null 则使用页面默认列宽。</param>
    /// <returns>对应的 <see cref="Column"/> 实例。</returns>
    public Column EnsureColumn(int columnIndex, float? width = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);

        var existing = _columns.FirstOrDefault(c => c.Index == columnIndex);
        if (existing is not null) return existing;

        var newCol = new Column(columnIndex, width ?? Settings.DefaultColumnWidth);
        _columns.Add(newCol);
        return newCol;
    }

    /// <summary>
    /// 在指定坐标处查找单元格。
    /// </summary>
    /// <param name="row">0 基行索引。</param>
    /// <param name="column">0 基列索引。</param>
    /// <returns>匹配的 <see cref="Cell"/>，若不存在则返回 null。</returns>
    public Cell? FindCell(int row, int column)
    {
        return _cells.FirstOrDefault(c => c.Row == row && c.Column == column);
    }

    /// <summary>
    /// 根据 Excel A1 格式地址查找单元格。
    /// </summary>
    /// <param name="address">A1 单元格地址（例如 "A1" 或 "B2"）。</param>
    /// <returns>匹配的 <see cref="Cell"/>，若不存在则返回 null。</returns>
    public Cell? FindCell(string address)
    {
        var (row, col) = CellReference.ParseCell(address);
        return FindCell(row, col);
    }

    /// <summary>
    /// 添加或替换指定坐标的单元格。
    /// </summary>
    /// <param name="cell">要添加或替换的单元格。</param>
    public void SetCell(Cell cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        int index = _cells.FindIndex(c => c.Row == cell.Row && c.Column == cell.Column);
        if (index >= 0)
        {
            _cells[index] = cell;
        }
        else
        {
            _cells.Add(cell);
        }
    }
}
