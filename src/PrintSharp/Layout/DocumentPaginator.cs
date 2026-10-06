using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Grid;

namespace PrintSharp.Layout;

/// <summary>根据纸张、余白和实际行高生成物理页，不修改原始文档。</summary>
public sealed class DocumentPaginator
{
    /// <summary>默认分页器。</summary>
    public static DocumentPaginator Instance { get; } = new();

    /// <summary>生成可直接导出的物理页文档；跨行合并单元格保持完整。</summary>
    public Document Paginate(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Pages.All(p => p.Pagination is null && p.Cells.All(c => c.Value is not PageValue)))
            return document;
        var result = new Document { Metadata = document.Metadata with { } };
        result.Pages.Clear();
        var contexts = new List<(Page Source, Page Target, int Offset, int Count)>();
        foreach (var source in document.Pages)
        {
            int rowCount = GetRowCount(source);
            var pagination = source.Pagination;
            if (pagination is null)
            {
                var target = source.Cells.Any(c => c.Value is PageValue)
                    ? CopyPage(source, Enumerable.Range(0, rowCount)) : source;
                contexts.Add((source, target, 0, rowCount));
                result.Pages.Add(target);
                continue;
            }
            int header = pagination.HeaderRowCount, footer = pagination.FooterRowCount;
            if (header < 0 || footer < 0 || (long)header + footer > rowCount)
                throw new InvalidOperationException("Pagination header/footer row counts exceed the page's row range.");
            int bodyEnd = rowCount - footer;
            foreach (var cell in source.Cells)
            {
                long end = (long)cell.Row + cell.RowSpan;
                if ((cell.Row < header && end > header) || (cell.Row < bodyEnd && end > bodyEnd))
                    throw new InvalidOperationException("A merged cell crosses a header/body/footer boundary.");
            }
            var heights = GetRowHeights(source, rowCount);
            var (width, height) = source.Settings.GetPaperSize();
            if (source.Settings.PaperKind != PaperKind.Customer && source.Settings.Orientation == PageOrientation.Landscape)
                (width, height) = (height, width);
            var margins = source.Settings.Margins;
            if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0 || height <= 0 ||
                !float.IsFinite(margins.Left) || !float.IsFinite(margins.Right) ||
                !float.IsFinite(margins.Top) || !float.IsFinite(margins.Bottom) ||
                margins.Left < 0 || margins.Right < 0 || margins.Top < 0 || margins.Bottom < 0)
                throw new InvalidOperationException("Automatic pagination requires finite positive paper dimensions and finite nonnegative margins.");
            double fixedHeight = heights.Take(header).Sum(h => (double)h) + heights.Skip(bodyEnd).Sum(h => (double)h);
            double available = height - margins.Top - margins.Bottom - fixedHeight;
            if (available < 0)
                throw new InvalidOperationException("Repeated header and footer exceed the printable page height.");
            var groupEnds = Enumerable.Range(0, rowCount).Select(r => r + 1).ToArray();
            foreach (var cell in source.Cells)
                groupEnds[cell.Row] = Math.Max(groupEnds[cell.Row], cell.Row + cell.RowSpan);
            int start = header;
            do
            {
                int end = start;
                double used = 0;
                while (end < bodyEnd)
                {
                    int groupEnd = groupEnds[end];
                    for (int r = end + 1; r < groupEnd; r++)
                        groupEnd = Math.Max(groupEnd, groupEnds[r]);
                    double groupHeight = 0;
                    for (int r = end; r < groupEnd; r++) groupHeight += heights[r];
                    if (groupHeight > available + 0.001)
                        throw new InvalidOperationException($"Body row group starting at row {end} exceeds the printable page height.");
                    if (used + groupHeight > available + 0.001) break;
                    used += groupHeight;
                    end = groupEnd;
                }
                var rows = Enumerable.Range(0, header).Concat(Enumerable.Range(start, end - start))
                    .Concat(Enumerable.Range(bodyEnd, footer));
                var target = CopyPage(source, rows);
                var bounds = GridLayoutEngine.Instance.CalculatePage(target);
                if (!float.IsFinite(bounds.ContentWidth) || bounds.ContentWidth > width - margins.Left - margins.Right + 0.001f)
                    throw new InvalidOperationException("Report columns exceed the printable page width.");
                contexts.Add((source, target, start - header, end - start));
                result.Pages.Add(target);
                start = end;
            } while (start < bodyEnd);
        }
        for (int i = 0; i < contexts.Count; i++)
        {
            var (source, target, offset, count) = contexts[i];
            if (source.Pagination is not null)
            {
                target.PageNumber = i + 1;
                string suffix = $"-{i + 1}";
                string name = source.Name ?? string.Empty;
                target.Name = name[..Math.Min(name.Length, 31 - suffix.Length)] + suffix;
            }
            var context = new PageContext
            {
                SourcePage = source, CurrentPageNumber = i + 1, PageCount = contexts.Count,
                BodyRowOffset = offset, BodyRowCount = count
            };
            if (!ReferenceEquals(source, target)) target.Context = context;
            for (int c = 0; c < target.Cells.Count; c++)
            {
                var cell = target.Cells[c];
                if (cell.Value is PageValue value)
                    target.Cells[c] = new Cell(cell.Row, cell.Column, value.Resolve(context),
                        cell.RowSpan, cell.ColumnSpan, cell.Type, cell.Style);
            }
        }
        return result;
    }

    internal static int GetRowCount(Page page) => Math.Max(
        page.Rows.Count == 0 ? 0 : page.Rows.Max(r => checked(r.Index + 1)),
        page.Cells.Count == 0 ? 0 : page.Cells.Max(c => checked(c.Row + c.RowSpan)));

    private static float[] GetRowHeights(Page page, int count)
    {
        var lookup = page.Rows.ToDictionary(r => r.Index);
        var heights = new float[count];
        for (int r = 0; r < count; r++)
        {
            heights[r] = lookup.TryGetValue(r, out var row)
                ? (row.IsHidden ? 0 : row.Height) : page.Settings.DefaultRowHeight;
            if (!float.IsFinite(heights[r]) || heights[r] < 0)
                throw new InvalidOperationException("Pagination requires finite nonnegative row heights.");
        }
        return heights;
    }

    private static Page CopyPage(Page source, IEnumerable<int> rows)
    {
        var target = new Page(source.Name, source.PageNumber) { Settings = source.Settings };
        foreach (var col in source.Columns)
            target.Columns.Add(new Column(col.Index, col.Width, col.DefaultStyle) { IsHidden = col.IsHidden });
        var map = rows.Select((original, index) => (original, index)).ToDictionary(p => p.original, p => p.index);
        foreach (var row in source.Rows)
            if (map.TryGetValue(row.Index, out int index))
                target.Rows.Add(new Row(index, row.Height, row.DefaultStyle) { IsHidden = row.IsHidden });
        foreach (var cell in source.Cells)
            if (map.TryGetValue(cell.Row, out int index))
                target.Cells.Add(new Cell(index, cell.Column, cell.Value, cell.RowSpan, cell.ColumnSpan, cell.Type, cell.Style));
        return target;
    }
}
