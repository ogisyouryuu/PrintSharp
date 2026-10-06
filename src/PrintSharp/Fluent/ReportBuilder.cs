using PrintSharp.Documents;
using PrintSharp.Grid;
using PrintSharp.Cells;
using PrintSharp.Layout;

namespace PrintSharp.Fluent;

/// <summary>声明重复页头、自动分页明细和重复页尾，各区域的行索引从 0 开始。</summary>
public sealed class ReportBuilder
{
    private readonly Page _page;
    private readonly Page _header;
    private readonly Page _body;
    private readonly Page _footer;
    internal ReportBuilder(Page page)
    {
        _page = page;
        _header = new Page { Settings = page.Settings };
        _body = new Page { Settings = page.Settings };
        _footer = new Page { Settings = page.Settings };
    }
    /// <summary>设置每页重复的页头。</summary>
    public ReportBuilder Header(Action<PageBuilder> configure) => Configure(_header, configure);
    /// <summary>设置自动分页的明细。</summary>
    public ReportBuilder Body(Action<PageBuilder> configure) => Configure(_body, configure);
    /// <summary>为每条记录构建一行明细，页容量由库计算。</summary>
    public ReportBuilder Body<T>(IEnumerable<T> items, float rowHeight, Action<RowBuilder, T> configureRow)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(configureRow);
        return Body(p => p.Table(t => t.Rows(items, rowHeight, configureRow)));
    }
    /// <summary>设置每页重复的页尾。</summary>
    public ReportBuilder Footer(Action<PageBuilder> configure) => Configure(_footer, configure);
    private ReportBuilder Configure(Page section, Action<PageBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        section.Rows.Clear(); section.Cells.Clear();
        configure(new PageBuilder(section));
        if (section.Settings != _page.Settings || section.Columns.Count != 0 || section.Pagination is not null)
            throw new InvalidOperationException("Configure paper, columns and pagination on the report page, not inside a report section.");
        return this;
    }
    internal void Build()
    {
        if (_page.Rows.Count != 0 || _page.Cells.Count != 0)
            throw new InvalidOperationException("Report must occupy an empty logical page; put existing rows in Header, Body or Footer.");
        int offset = 0;
        foreach (var section in new[] { _header, _body, _footer })
        {
            foreach (var row in section.Rows)
                _page.Rows.Add(new Row(row.Index + offset, row.Height, row.DefaultStyle) { IsHidden = row.IsHidden });
            foreach (var cell in section.Cells)
                _page.Cells.Add(new Cell(cell.Row + offset, cell.Column, cell.Value, cell.RowSpan, cell.ColumnSpan, cell.Type, cell.Style));
            offset += DocumentPaginator.GetRowCount(section);
        }
        _page.Pagination = new PaginationSettings
        {
            HeaderRowCount = DocumentPaginator.GetRowCount(_header),
            FooterRowCount = DocumentPaginator.GetRowCount(_footer)
        };
    }
}
