using PrintSharp.Cells;
using PrintSharp.Styles;

namespace PrintSharp.Fluent;

/// <summary>
/// 按行顺序流式定义单元格的 Fluent 构建器。
/// 自动递增当前行内的列指针，便于快速构建表格行。
/// </summary>
public sealed class RowBuilder
{
    private readonly int _rowIndex;
    private int _currentColumn;
    private readonly List<CellBuilder> _cellBuilders = [];
    private StyleBuilder? _rowStyleBuilder;

    /// <summary>
    /// 获取当前行的 0 基索引。
    /// </summary>
    public int RowIndex => _rowIndex;

    /// <summary>
    /// 获取当前行指针所在的 0 基列索引。
    /// </summary>
    public int CurrentColumn => _currentColumn;

    /// <summary>
    /// 初始化 <see cref="RowBuilder"/> 类的新实例。
    /// </summary>
    /// <param name="rowIndex">0 基行索引。</param>
    /// <param name="startColumn">起始列索引，默认为 0。</param>
    public RowBuilder(int rowIndex, int startColumn = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(startColumn);
        _rowIndex = rowIndex;
        _currentColumn = startColumn;
    }

    /// <summary>
    /// 在当前列添加一个单元格并返回其配置构建器，列指针自动后移 1 位。
    /// </summary>
    /// <param name="value">单元格值。</param>
    /// <returns>单元格构建器。</returns>
    public CellBuilder Cell(object? value)
    {
        var builder = new CellBuilder(_rowIndex, _currentColumn).Value(value);
        _cellBuilders.Add(builder);
        _currentColumn++;
        return builder;
    }

    /// <summary>
    /// 在当前列添加一个单元格并使用委托配置，列指针根据单元格的跨列数自动后移。
    /// </summary>
    /// <param name="value">单元格值。</param>
    /// <param name="configure">配置委托。</param>
    /// <returns>当前 <see cref="RowBuilder"/> 实例，支持链式调用。</returns>
    public RowBuilder Cell(object? value, Action<CellBuilder> configure)
    {
        var builder = new CellBuilder(_rowIndex, _currentColumn).Value(value);
        configure(builder);
        _cellBuilders.Add(builder);
        _currentColumn += builder.ColumnSpanCount;
        return this;
    }

    /// <summary>
    /// 在当前列添加一个单元格并使用委托配置，列指针根据跨列数自动后移。
    /// </summary>
    /// <param name="configure">配置委托。</param>
    /// <returns>当前 <see cref="RowBuilder"/> 实例，支持链式调用。</returns>
    public RowBuilder Cell(Action<CellBuilder> configure)
    {
        var builder = new CellBuilder(_rowIndex, _currentColumn);
        configure(builder);
        _cellBuilders.Add(builder);
        _currentColumn += builder.ColumnSpanCount;
        return this;
    }

    /// <summary>
    /// 跳过指定数量的列（使后续单元格偏移）。
    /// </summary>
    /// <param name="count">跳过的列数，默认为 1。</param>
    public RowBuilder Skip(int count = 1)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        _currentColumn += count;
        return this;
    }

    /// <summary>
    /// 手动指定下一个单元格的列索引。
    /// </summary>
    /// <param name="columnIndex">目标 0 基列索引。</param>
    public RowBuilder AtColumn(int columnIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        _currentColumn = columnIndex;
        return this;
    }

    /// <summary>
    /// 为整行设置默认样式。
    /// </summary>
    public RowBuilder DefaultStyle(Action<StyleBuilder> configure)
    {
        _rowStyleBuilder ??= new StyleBuilder();
        configure(_rowStyleBuilder);
        return this;
    }

    /// <summary>
    /// 构建行默认样式。
    /// </summary>
    internal CellStyle? BuildRowStyle() => _rowStyleBuilder?.Build();

    /// <summary>
    /// 构建本行中定义的所有单元格。
    /// </summary>
    internal IEnumerable<Cell> BuildCells()
    {
        return _cellBuilders.Select(b => b.Build());
    }
}
