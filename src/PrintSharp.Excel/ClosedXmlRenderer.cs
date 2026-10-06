using ClosedXML.Excel;
using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Styles;

namespace PrintSharp.Excel;

/// <summary>
/// 基于 ClosedXML 的 Excel 渲染器。
/// 将 PrintSharp 网格文档模型忠实渲染为 Excel 工作簿文件。
/// </summary>
public sealed class ClosedXmlRenderer
{
    /// <summary>
    /// 获取全局默认渲染器单例。
    /// </summary>
    public static ClosedXmlRenderer Instance { get; } = new();

    /// <summary>
    /// 将 PrintSharp 网格文档渲染为 ClosedXML <see cref="XLWorkbook"/> 实例。
    /// </summary>
    /// <param name="document">待渲染的网格文档。</param>
    /// <param name="options">渲染选项（可选）。</param>
    /// <returns>构建完成的 <see cref="XLWorkbook"/> 实例。</returns>
    public XLWorkbook RenderToWorkbook(Document document, ExcelRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= ExcelRenderOptions.Default;

        var workbook = new XLWorkbook();

        // 写入元数据
        if (!string.IsNullOrEmpty(document.Metadata.Title))
            workbook.Properties.Title = document.Metadata.Title;
        if (!string.IsNullOrEmpty(document.Metadata.Author))
            workbook.Properties.Author = document.Metadata.Author;
        if (!string.IsNullOrEmpty(document.Metadata.Subject))
            workbook.Properties.Subject = document.Metadata.Subject;
        if (!string.IsNullOrEmpty(document.Metadata.Keywords))
            workbook.Properties.Keywords = document.Metadata.Keywords;

        // 渲染各个页面（Worksheet）
        foreach (var page in document.Pages)
        {
            RenderPageToWorksheet(page, workbook, options);
        }

        return workbook;
    }

    /// <summary>
    /// 将网格文档渲染并写入目标数据流。
    /// </summary>
    /// <param name="document">网格文档。</param>
    /// <param name="outputStream">目标输出流。</param>
    /// <param name="options">渲染选项（可选）。</param>
    public void Render(Document document, Stream outputStream, ExcelRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(outputStream);

        using var workbook = RenderToWorkbook(document, options);
        workbook.SaveAs(outputStream);
    }

    /// <summary>
    /// 将网格文档渲染并保存到指定文件路径。
    /// </summary>
    /// <param name="document">网格文档。</param>
    /// <param name="filePath">输出文件路径。</param>
    /// <param name="options">渲染选项（可选）。</param>
    public void Render(Document document, string filePath, ExcelRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        using var workbook = RenderToWorkbook(document, options);
        workbook.SaveAs(filePath);
    }

    /// <summary>
    /// 将网格文档渲染为 Excel 二进制字节数组（.xlsx）。
    /// </summary>
    /// <param name="document">网格文档。</param>
    /// <param name="options">渲染选项（可选）。</param>
    /// <returns>Excel 文件的二进制数据。</returns>
    public byte[] RenderToBytes(Document document, ExcelRenderOptions? options = null)
    {
        using var ms = new MemoryStream();
        Render(document, ms, options);
        return ms.ToArray();
    }

    private static void RenderPageToWorksheet(Page page, XLWorkbook workbook, ExcelRenderOptions options)
    {
        string sheetName = string.IsNullOrWhiteSpace(page.Name) ? $"Sheet{page.PageNumber}" : page.Name;
        // 保证工作表名不重复
        int count = 1;
        string uniqueName = sheetName;
        while (workbook.Worksheets.Contains(uniqueName))
        {
            uniqueName = $"{sheetName}_{count++}";
        }

        var ws = workbook.Worksheets.Add(uniqueName);

        if (page.Settings.PaperKind != PaperKind.Customer)
            ws.PageSetup.PaperSize = page.Settings.PaperKind switch
            {
                PaperKind.A5 => XLPaperSize.A5Paper,
                PaperKind.B5 => XLPaperSize.B5Paper,
                PaperKind.A4 => XLPaperSize.A4Paper,
                PaperKind.B4 => XLPaperSize.B4Paper,
                PaperKind.A3 => XLPaperSize.A3Paper,
                _ => throw new ArgumentOutOfRangeException(nameof(page.Settings.PaperKind))
            };

        // 设置页面方向与打印参数
        ws.PageSetup.PageOrientation = page.Settings.Orientation == PageOrientation.Landscape
            ? XLPageOrientation.Landscape
            : XLPageOrientation.Portrait;

        // 设置列宽
        foreach (var col in page.Columns)
        {
            var xlCol = ws.Column(col.Index + 1);
            if (col.Width > 0)
            {
                xlCol.Width = Math.Max(1.0, col.Width * options.ColumnWidthScale);
            }
            if (col.IsHidden)
            {
                xlCol.Hide();
            }
        }

        // 设置行高
        foreach (var row in page.Rows)
        {
            var xlRow = ws.Row(row.Index + 1);
            if (row.Height > 0)
            {
                xlRow.Height = Math.Max(1.0, row.Height * options.RowHeightScale);
            }
            if (row.IsHidden)
            {
                xlRow.Hide();
            }
        }

        // 渲染单元格
        int pictureIndex = 1;
        foreach (var cell in page.Cells)
        {
            int startRow = cell.Row + 1;
            int startCol = cell.Column + 1;
            int endRow = startRow + cell.RowSpan - 1;
            int endCol = startCol + cell.ColumnSpan - 1;

            var xlRange = ws.Range(startRow, startCol, endRow, endCol);
            var xlCell = ws.Cell(startRow, startCol);

            // 合并单元格
            if (cell.IsMerged)
            {
                xlRange.Merge();
            }

            // 写入值与类型
            ApplyCellValue(xlCell, cell, ws, ref pictureIndex);

            // 写入样式
            if (cell.Style is not null)
            {
                ApplyCellStyle(xlRange, cell.Style);
            }
        }

        if (options.AutoFitColumns)
        {
            ws.Columns().AdjustToContents();
        }
    }

    private static void ApplyCellValue(IXLCell xlCell, Cell cell, IXLWorksheet ws, ref int pictureIndex)
    {
        if (cell.Type == CellType.Formula)
        {
            string formula = cell.Value?.ToString() ?? "";
            xlCell.FormulaA1 = formula.StartsWith('=') ? formula[1..] : formula;
            return;
        }

        if (cell.Type == CellType.Image)
        {
            if (cell.Value is byte[] imgBytes && imgBytes.Length > 0)
            {
                using var ms = new MemoryStream(imgBytes);
                string picName = $"Picture_{pictureIndex++}";
                var picture = ws.AddPicture(ms, picName);
                picture.MoveTo(xlCell);
            }
            return;
        }

        if (cell.Value is null)
        {
            return;
        }

        switch (cell.Value)
        {
            case string str:
                xlCell.SetValue(str);
                break;
            case int i:
                xlCell.SetValue(i);
                break;
            case long l:
                xlCell.SetValue(l);
                break;
            case short s:
                xlCell.SetValue(s);
                break;
            case byte b:
                xlCell.SetValue(b);
                break;
            case decimal dec:
                xlCell.SetValue(dec);
                break;
            case double d:
                xlCell.SetValue(d);
                break;
            case float f:
                xlCell.SetValue(f);
                break;
            case bool boolean:
                xlCell.SetValue(boolean);
                break;
            case DateTime dt:
                xlCell.SetValue(dt);
                break;
            case DateTimeOffset dto:
                xlCell.SetValue(dto.DateTime);
                break;
            case TimeSpan ts:
                xlCell.SetValue(ts);
                break;
            default:
                xlCell.SetValue(cell.Value.ToString() ?? "");
                break;
        }
    }

    private static void ApplyCellStyle(IXLRange xlRange, CellStyle style)
    {
        // 1. 字体
        if (style.Font is not null)
        {
            if (!string.IsNullOrWhiteSpace(style.Font.Family))
                xlRange.Style.Font.FontName = style.Font.Family;

            if (style.Font.Size > 0)
                xlRange.Style.Font.FontSize = style.Font.Size;

            xlRange.Style.Font.Bold = style.Font.Bold;
            xlRange.Style.Font.Italic = style.Font.Italic;

            if (style.Font.Underline)
                xlRange.Style.Font.Underline = XLFontUnderlineValues.Single;

            if (style.Font.StrikeThrough)
                xlRange.Style.Font.Strikethrough = true;
        }

        // 2. 前景色（文字颜色）
        if (style.ForeColor.HasValue)
        {
            var fc = style.ForeColor.Value;
            xlRange.Style.Font.FontColor = XLColor.FromArgb(fc.A, fc.R, fc.G, fc.B);
        }

        // 背景色を単色塗りつぶしとして設定する
        if (style.BackColor.HasValue && style.BackColor.Value.A > 0)
        {
            var bc = style.BackColor.Value;
            xlRange.Style.Fill.PatternType = XLFillPatternValues.Solid;
            xlRange.Style.Fill.BackgroundColor = XLColor.FromArgb(bc.A, bc.R, bc.G, bc.B);
        }

        // 4. 对齐
        switch (style.HorizontalAlignment)
        {
            case HorizontalAlignment.Left:
                xlRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                break;
            case HorizontalAlignment.Center:
                xlRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                break;
            case HorizontalAlignment.Right:
                xlRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                break;
            case HorizontalAlignment.Justify:
                xlRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Justify;
                break;
            default:
                break;
        }

        switch (style.VerticalAlignment)
        {
            case VerticalAlignment.Top:
                xlRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                break;
            case VerticalAlignment.Middle:
                xlRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                break;
            case VerticalAlignment.Bottom:
                xlRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Bottom;
                break;
        }

        if (style.WrapText)
        {
            xlRange.Style.Alignment.WrapText = true;
        }

        // 5. 格式化
        if (!string.IsNullOrWhiteSpace(style.Format))
        {
            xlRange.Style.NumberFormat.Format = style.Format;
        }

        // 6. 边框
        if (style.Border is not null)
        {
            ApplyBorderLine(style.Border.Top,
                s => xlRange.Style.Border.TopBorder = s,
                c => xlRange.Style.Border.TopBorderColor = c);

            ApplyBorderLine(style.Border.Bottom,
                s => xlRange.Style.Border.BottomBorder = s,
                c => xlRange.Style.Border.BottomBorderColor = c);

            ApplyBorderLine(style.Border.Left,
                s => xlRange.Style.Border.LeftBorder = s,
                c => xlRange.Style.Border.LeftBorderColor = c);

            ApplyBorderLine(style.Border.Right,
                s => xlRange.Style.Border.RightBorder = s,
                c => xlRange.Style.Border.RightBorderColor = c);
        }
    }

    private static void ApplyBorderLine(
        BorderLine? line,
        Action<XLBorderStyleValues> setStyle,
        Action<XLColor> setColor)
    {
        if (line is null || line.Style == BorderStyle.None)
        {
            return;
        }

        var xlStyle = line.Style switch
        {
            BorderStyle.Thin => XLBorderStyleValues.Thin,
            BorderStyle.Medium => XLBorderStyleValues.Medium,
            BorderStyle.Dashed => XLBorderStyleValues.Dashed,
            BorderStyle.Dotted => XLBorderStyleValues.Dotted,
            BorderStyle.Thick => XLBorderStyleValues.Thick,
            BorderStyle.Double => XLBorderStyleValues.Double,
            BorderStyle.Hair => XLBorderStyleValues.Hair,
            _ => XLBorderStyleValues.None
        };

        setStyle(xlStyle);

        if (line.Color.HasValue)
        {
            var c = line.Color.Value;
            setColor(XLColor.FromArgb(c.A, c.R, c.G, c.B));
        }
    }
}
