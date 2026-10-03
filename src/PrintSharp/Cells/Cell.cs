using PrintSharp.Styles;

namespace PrintSharp.Cells;

/// <summary>
/// 表示网格文档中的一个单元格。位置严格由网格坐标（行、列及合并跨度）决定。
/// </summary>
public sealed class Cell
{
    private int _row;
    private int _column;
    private int _rowSpan = 1;
    private int _columnSpan = 1;

    /// <summary>
    /// 获取或设置 0 基行索引（第一行为 0）。
    /// </summary>
    public int Row
    {
        get => _row;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _row = value;
        }
    }

    /// <summary>
    /// 获取或设置 0 基列索引（第一列为 0）。
    /// </summary>
    public int Column
    {
        get => _column;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _column = value;
        }
    }

    /// <summary>
    /// 获取或设置跨行数（合并行数，最小为 1）。
    /// </summary>
    public int RowSpan
    {
        get => _rowSpan;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "RowSpan must be greater than or equal to 1.");
            }
            _rowSpan = value;
        }
    }

    /// <summary>
    /// 获取或设置跨列数（合并列数，最小为 1）。
    /// </summary>
    public int ColumnSpan
    {
        get => _columnSpan;
        init
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "ColumnSpan must be greater than or equal to 1.");
            }
            _columnSpan = value;
        }
    }

    /// <summary>
    /// 获取或设置单元格类型。
    /// </summary>
    public CellType Type { get; init; } = CellType.Text;

    /// <summary>
    /// 获取或设置单元格的逻辑值（如 string、int、decimal、DateTime、byte[] 等）。
    /// </summary>
    public object? Value { get; init; }

    /// <summary>
    /// 获取或设置单元格样式。
    /// </summary>
    public CellStyle? Style { get; init; }

    /// <summary>
    /// 获取当前单元格是否为合并单元格（跨行或跨列大于 1）。
    /// </summary>
    public bool IsMerged => RowSpan > 1 || ColumnSpan > 1;

    /// <summary>
    /// 获取 Excel A1 格式的单元格或区域引用地址（例如 "A1" 或 "B2:C3"）。
    /// </summary>
    public string Address => CellReference.FormatRange(Row, Column, RowSpan, ColumnSpan);

    /// <summary>
    /// 初始化 <see cref="Cell"/> 类的新实例。
    /// </summary>
    public Cell() { }

    /// <summary>
    /// 初始化 <see cref="Cell"/> 类的新实例。
    /// </summary>
    /// <param name="row">0 基行索引。</param>
    /// <param name="column">0 基列索引。</param>
    /// <param name="value">单元格值。</param>
    /// <param name="rowSpan">跨行数。</param>
    /// <param name="columnSpan">跨列数。</param>
    /// <param name="type">单元格类型。</param>
    /// <param name="style">单元格样式。</param>
    public Cell(
        int row,
        int column,
        object? value = null,
        int rowSpan = 1,
        int columnSpan = 1,
        CellType type = CellType.Text,
        CellStyle? style = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(row);
        ArgumentOutOfRangeException.ThrowIfNegative(column);
        if (rowSpan < 1) throw new ArgumentOutOfRangeException(nameof(rowSpan), "RowSpan must be >= 1.");
        if (columnSpan < 1) throw new ArgumentOutOfRangeException(nameof(columnSpan), "ColumnSpan must be >= 1.");

        _row = row;
        _column = column;
        _rowSpan = rowSpan;
        _columnSpan = columnSpan;
        Value = value;
        Type = type;
        Style = style;
    }

    /// <inheritdoc/>
    public override string ToString() => $"Cell[{Address}] = {Value}";
}
