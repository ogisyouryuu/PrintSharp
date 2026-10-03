using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Grid;
using PrintSharp.Styles;

namespace PrintSharp.Layout;

/// <summary>
/// 默认网格布局计算引擎。根据 Excel Grid 规则精确计算每个单元格的物理坐标与包围盒。
/// </summary>
public sealed class GridLayoutEngine : ILayoutEngine
{
    /// <summary>
    /// 获取全局默认布局引擎单例。
    /// </summary>
    public static GridLayoutEngine Instance { get; } = new();

    /// <inheritdoc/>
    public CalculatedDocumentLayout CalculateDocument(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var calculatedPages = new List<CalculatedPageLayout>(document.Pages.Count);
        foreach (var page in document.Pages)
        {
            calculatedPages.Add(CalculatePage(page));
        }

        return new CalculatedDocumentLayout(document, calculatedPages);
    }

    /// <inheritdoc/>
    public CalculatedPageLayout CalculatePage(Page page)
    {
        ArgumentNullException.ThrowIfNull(page);

        var settings = page.Settings ?? PageSettings.Default;
        var margins = settings.Margins;

        // 1. 计算最大行数和最大列数
        int maxRow = 0;
        int maxCol = 0;

        foreach (var row in page.Rows)
        {
            if (row.Index > maxRow) maxRow = row.Index;
        }

        foreach (var col in page.Columns)
        {
            if (col.Index > maxCol) maxCol = col.Index;
        }

        foreach (var cell in page.Cells)
        {
            int cellMaxRow = cell.Row + cell.RowSpan - 1;
            int cellMaxCol = cell.Column + cell.ColumnSpan - 1;
            if (cellMaxRow > maxRow) maxRow = cellMaxRow;
            if (cellMaxCol > maxCol) maxCol = cellMaxCol;
        }

        int rowCount = page.Cells.Count > 0 || page.Rows.Count > 0 ? maxRow + 1 : 0;
        int colCount = page.Cells.Count > 0 || page.Columns.Count > 0 ? maxCol + 1 : 0;

        // 2. 映射每行高度与行起始 Y 偏移
        var rowLookup = page.Rows.ToDictionary(r => r.Index);
        var rowHeights = new float[rowCount];
        var rowYOffsets = new float[rowCount];

        float currentY = margins.Top;
        for (int r = 0; r < rowCount; r++)
        {
            rowYOffsets[r] = currentY;
            float h = rowLookup.TryGetValue(r, out var rowDef)
                ? (rowDef.IsHidden ? 0f : rowDef.Height)
                : settings.DefaultRowHeight;
            rowHeights[r] = h;
            currentY += h;
        }
        float contentHeight = currentY - margins.Top;

        // 3. 映射每列宽度与列起始 X 偏移
        var colLookup = page.Columns.ToDictionary(c => c.Index);
        var colWidths = new float[colCount];
        var colXOffsets = new float[colCount];

        float currentX = margins.Left;
        for (int c = 0; c < colCount; c++)
        {
            colXOffsets[c] = currentX;
            float w = colLookup.TryGetValue(c, out var colDef)
                ? (colDef.IsHidden ? 0f : colDef.Width)
                : settings.DefaultColumnWidth;
            colWidths[c] = w;
            currentX += w;
        }
        float contentWidth = currentX - margins.Left;

        // 4. 计算每个单元格的物理位置与有效样式
        var calculatedCells = new List<CalculatedCellLayout>(page.Cells.Count);
        foreach (var cell in page.Cells)
        {
            float cellX = cell.Column < colCount ? colXOffsets[cell.Column] : margins.Left;
            float cellY = cell.Row < rowCount ? rowYOffsets[cell.Row] : margins.Top;

            // 计算跨度总宽度
            float cellWidth = 0f;
            for (int c = 0; c < cell.ColumnSpan; c++)
            {
                int colIdx = cell.Column + c;
                if (colIdx < colCount)
                {
                    cellWidth += colWidths[colIdx];
                }
                else
                {
                    cellWidth += settings.DefaultColumnWidth;
                }
            }

            // 计算跨度总高度
            float cellHeight = 0f;
            for (int r = 0; r < cell.RowSpan; r++)
            {
                int rowIdx = cell.Row + r;
                if (rowIdx < rowCount)
                {
                    cellHeight += rowHeights[rowIdx];
                }
                else
                {
                    cellHeight += settings.DefaultRowHeight;
                }
            }

            var bounds = new LayoutRect(cellX, cellY, cellWidth, cellHeight);

            // 合并样式：列默认样式 -> 行默认样式 -> 单元格样式
            var effectiveStyle = CellStyle.Default;
            if (colLookup.TryGetValue(cell.Column, out var colDef) && colDef.DefaultStyle is not null)
            {
                effectiveStyle = effectiveStyle.MergeWith(colDef.DefaultStyle);
            }
            if (rowLookup.TryGetValue(cell.Row, out var rowDef) && rowDef.DefaultStyle is not null)
            {
                effectiveStyle = effectiveStyle.MergeWith(rowDef.DefaultStyle);
            }
            if (cell.Style is not null)
            {
                effectiveStyle = effectiveStyle.MergeWith(cell.Style);
            }

            // 计算去除内边距后的实际内容矩形
            var padding = effectiveStyle.Padding ?? PaddingSpec.Zero;
            float contentX = cellX + padding.Left;
            float contentY = cellY + padding.Top;
            float contentW = Math.Max(0, cellWidth - padding.Horizontal);
            float contentH = Math.Max(0, cellHeight - padding.Vertical);
            var contentBounds = new LayoutRect(contentX, contentY, contentW, contentH);

            calculatedCells.Add(new CalculatedCellLayout(cell, bounds, contentBounds, effectiveStyle));
        }

        float totalWidth = Math.Max(settings.Width, margins.Left + contentWidth + margins.Right);
        float totalHeight = Math.Max(settings.Height, margins.Top + contentHeight + margins.Bottom);

        return new CalculatedPageLayout(
            page,
            contentWidth,
            contentHeight,
            totalWidth,
            totalHeight,
            calculatedCells);
    }
}
