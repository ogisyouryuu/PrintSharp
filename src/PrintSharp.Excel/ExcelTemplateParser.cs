using ClosedXML.Excel;
using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Grid;
using PrintSharp.Styles;
using System.Collections;
using System.Text.RegularExpressions;

namespace PrintSharp.Excel;

/// <summary>
/// Excel 模板解析与数据填充引擎。
/// 支持将现有 Excel 模板读取为 PrintSharp 网格文档，并支持基于占位符（{{Property}}）的数据绑定与行展开。
/// </summary>
public sealed partial class ExcelTemplateParser
{
    private static readonly Regex PlaceholderRegex = CreatePlaceholderRegex();
    private readonly ITemplateValueResolver _valueResolver;

    [GeneratedRegex(@"\{\{([^}]+)\}\}")]
    private static partial Regex CreatePlaceholderRegex();

    /// <summary>
    /// 获取全局默认模板解析器单例。
    /// </summary>
    public static ExcelTemplateParser Instance { get; } = new();

    /// <summary>
    /// Creates an Excel template parser with the specified value resolver.
    /// </summary>
    /// <param name="valueResolver">Resolver used to access template data, or <see langword="null"/> for the default POCO and dictionary resolver.</param>
    public ExcelTemplateParser(ITemplateValueResolver? valueResolver = null)
    {
        _valueResolver = valueResolver ?? new DefaultTemplateValueResolver();
    }

    /// <summary>
    /// 解析 Excel 模板文件为 PrintSharp <see cref="Document"/> 网格模型。
    /// </summary>
    /// <param name="filePath">Excel 文件绝对或相对路径。</param>
    /// <param name="options">渲染与尺寸转换选项（可选）。</param>
    /// <returns>解析后的通用网格文档。</returns>
    public Document Parse(string filePath, ExcelRenderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var workbook = new XLWorkbook(filePath);
        return ParseWorkbook(workbook, options);
    }

    /// <summary>
    /// 从数据流中解析 Excel 模板为 PrintSharp <see cref="Document"/> 网格模型。
    /// </summary>
    /// <param name="stream">包含 Excel 数据的输入流。</param>
    /// <param name="options">渲染与尺寸转换选项（可选）。</param>
    /// <returns>解析后的通用网格文档。</returns>
    public Document Parse(Stream stream, ExcelRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var workbook = new XLWorkbook(stream);
        return ParseWorkbook(workbook, options);
    }

    /// <summary>
    /// 将 ClosedXML <see cref="XLWorkbook"/> 转换为 PrintSharp <see cref="Document"/>。
    /// </summary>
    public Document ParseWorkbook(XLWorkbook workbook, ExcelRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        options ??= ExcelRenderOptions.Default;

        var document = new Document();
        document.Pages.Clear(); // 清空默认初始页

        // 读取文档元数据
        document.Metadata = new DocumentMetadata
        {
            Title = workbook.Properties.Title,
            Author = workbook.Properties.Author,
            Subject = workbook.Properties.Subject,
            Keywords = workbook.Properties.Keywords
        };

        int pageNumber = 1;
        foreach (var ws in workbook.Worksheets)
        {
            var page = ParseWorksheet(ws, pageNumber++, options);
            document.Pages.Add(page);
        }

        if (document.Pages.Count == 0)
        {
            document.AddPage("Sheet1");
        }

        return document;
    }

    /// <summary>
    /// 解析单个工作表为 PrintSharp <see cref="Page"/>。
    /// </summary>
    public Page ParseWorksheet(IXLWorksheet ws, int pageNumber = 1, ExcelRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(ws);
        options ??= ExcelRenderOptions.Default;

        var page = new Page(ws.Name, pageNumber)
        {
            Settings = new PageSettings
            {
                Orientation = ws.PageSetup.PageOrientation == XLPageOrientation.Landscape
                    ? PageOrientation.Landscape
                    : PageOrientation.Portrait
            }
        };

        var rangeUsed = ws.RangeUsed();
        if (rangeUsed is null)
        {
            return page;
        }

        int firstCol = rangeUsed.FirstColumn().ColumnNumber();
        int lastCol = rangeUsed.LastColumn().ColumnNumber();
        int firstRow = rangeUsed.FirstRow().RowNumber();
        int lastRow = rangeUsed.LastRow().RowNumber();

        // 1. 读取列定义（列宽）
        for (int c = firstCol; c <= lastCol; c++)
        {
            var xlCol = ws.Column(c);
            float width = (float)(xlCol.Width / options.ColumnWidthScale);
            page.Columns.Add(new Column(c - 1, width) { IsHidden = xlCol.IsHidden });
        }

        // 2. 读取行定义（行高）
        for (int r = firstRow; r <= lastRow; r++)
        {
            var xlRow = ws.Row(r);
            float height = (float)(xlRow.Height / options.RowHeightScale);
            page.Rows.Add(new Row(r - 1, height) { IsHidden = xlRow.IsHidden });
        }

        // 3. 构建合并单元格映射表
        // key: (startRow, startCol), value: (rowSpan, colSpan)
        var mergedOrigins = new Dictionary<(int Row, int Col), (int RowSpan, int ColSpan)>();
        var mergedSecondaryCells = new HashSet<(int Row, int Col)>();

        foreach (var mRange in ws.MergedRanges)
        {
            int startR = mRange.RangeAddress.FirstAddress.RowNumber - 1;
            int startC = mRange.RangeAddress.FirstAddress.ColumnNumber - 1;
            int endR = mRange.RangeAddress.LastAddress.RowNumber - 1;
            int endC = mRange.RangeAddress.LastAddress.ColumnNumber - 1;

            int rowSpan = endR - startR + 1;
            int colSpan = endC - startC + 1;

            mergedOrigins[(startR, startC)] = (rowSpan, colSpan);

            for (int r = startR; r <= endR; r++)
            {
                for (int c = startC; c <= endC; c++)
                {
                    if (r != startR || c != startC)
                    {
                        mergedSecondaryCells.Add((r, c));
                    }
                }
            }
        }

        // 4. 读取单元格内容与样式
        for (int r = firstRow; r <= lastRow; r++)
        {
            for (int c = firstCol; c <= lastCol; c++)
            {
                int rIdx = r - 1;
                int cIdx = c - 1;

                // 跳过合并区域中的从属非左上角单元格
                if (mergedSecondaryCells.Contains((rIdx, cIdx)))
                {
                    continue;
                }

                var xlCell = ws.Cell(r, c);
                int rowSpan = 1;
                int colSpan = 1;

                if (mergedOrigins.TryGetValue((rIdx, cIdx), out var span))
                {
                    rowSpan = span.RowSpan;
                    colSpan = span.ColSpan;
                }

                var (value, cellType) = ExtractCellValue(xlCell);
                var style = ExtractCellStyle(xlCell);

                var cell = new Cell(rIdx, cIdx, value, rowSpan, colSpan, cellType, style);
                page.SetCell(cell);
            }
        }

        return page;
    }

    /// <summary>
    /// 读取 Excel 模板文件，填充数据模型后生成新的 PrintSharp <see cref="Document"/>。
    /// </summary>
    /// <param name="templatePath">模板文件路径。</param>
    /// <param name="data">数据源对象（支持 POCO、Dictionary、匿名对象等）。</param>
    /// <param name="options">渲染选项（可选）。</param>
    /// <returns>填充数据后的网格文档。</returns>
    public Document Render(string templatePath, object? data, ExcelRenderOptions? options = null)
    {
        using var stream = File.OpenRead(templatePath);
        return Render(stream, data, options);
    }

    /// <summary>
    /// 读取 Excel 模板流，填充数据模型后生成新的 PrintSharp <see cref="Document"/>。
    /// 支持单个占位符（例如 {{Customer.Name}}）及集合重复行展开（例如 {{#Items}} 或 {{Items.Prop}}）。
    /// </summary>
    /// <param name="templateStream">模板输入流。</param>
    /// <param name="data">数据源对象。</param>
    /// <param name="options">渲染选项（可选）。</param>
    /// <returns>填充数据后的网格文档。</returns>
    public Document Render(Stream templateStream, object? data, ExcelRenderOptions? options = null)
    {
        var rawDoc = Parse(templateStream, options);
        if (data is null) return rawDoc;

        var renderedDoc = new Document
        {
            Metadata = rawDoc.Metadata
        };
        renderedDoc.Pages.Clear();

        foreach (var rawPage in rawDoc.Pages)
        {
            var renderedPage = RenderPageData(rawPage, data);
            renderedDoc.Pages.Add(renderedPage);
        }

        return renderedDoc;
    }

    private Page RenderPageData(Page templatePage, object data)
    {
        var newPage = new Page(templatePage.Name, templatePage.PageNumber)
        {
            Settings = templatePage.Settings
        };

        // 复制列定义
        foreach (var col in templatePage.Columns)
        {
            newPage.Columns.Add(new Column(col.Index, col.Width, col.DefaultStyle) { IsHidden = col.IsHidden });
        }

        // 分组单元格按行处理
        var rowGroups = templatePage.Cells.GroupBy(c => c.Row).OrderBy(g => g.Key).ToList();
        var rowLookup = templatePage.Rows.ToDictionary(r => r.Index);

        int targetRow = 0;
        int maxTemplateRow = rowGroups.Count > 0 ? rowGroups.Max(g => g.Key) : -1;

        for (int r = 0; r <= maxTemplateRow; r++)
        {
            var cellsInRow = rowGroups.FirstOrDefault(g => g.Key == r)?.ToList();
            if (cellsInRow is null || cellsInRow.Count == 0)
            {
                if (rowLookup.TryGetValue(r, out var rDef))
                {
                    newPage.Rows.Add(new Row(targetRow, rDef.Height, rDef.DefaultStyle));
                }
                targetRow++;
                continue;
            }

            // 检查该行是否包含集合重复占位符（例如包含 "Items.Name" 且 data 中具有 Items 集合）
            var collectionRef = FindCollectionReference(cellsInRow, data);
            if (collectionRef is not null)
            {
                var (collectionName, items) = collectionRef.Value;
                float rowHeight = rowLookup.TryGetValue(r, out var rDef) ? rDef.Height : templatePage.Settings.DefaultRowHeight;

                foreach (var item in items)
                {
                    newPage.Rows.Add(new Row(targetRow, rowHeight));
                    foreach (var cell in cellsInRow)
                    {
                        var boundCell = BindCellWithItemContext(cell, targetRow, item, collectionName, data);
                        newPage.SetCell(boundCell);
                    }
                    targetRow++;
                }
            }
            else
            {
                // 普通行，替换普通占位符
                float rowHeight = rowLookup.TryGetValue(r, out var rDef) ? rDef.Height : templatePage.Settings.DefaultRowHeight;
                newPage.Rows.Add(new Row(targetRow, rowHeight));

                foreach (var cell in cellsInRow)
                {
                    var boundCell = BindCellGeneral(cell, targetRow, data);
                    newPage.SetCell(boundCell);
                }
                targetRow++;
            }
        }

        return newPage;
    }

    private (string CollectionName, IEnumerable<object> Items)? FindCollectionReference(
        List<Cell> cells, object rootData)
    {
        foreach (var cell in cells)
        {
            if (cell.Value is not string str) continue;

            var match = PlaceholderRegex.Match(str);
            while (match.Success)
            {
                string path = match.Groups[1].Value.Trim();
                int dotIdx = path.IndexOf('.');
                if (dotIdx > 0)
                {
                    string candidateCol = path[..dotIdx];
                    var val = _valueResolver.Resolve(rootData, candidateCol);
                    if (val is IEnumerable enumerable and not string &&
                        val is not IDictionary<string, object?> and not IDictionary)
                    {
                        var list = enumerable.Cast<object>().ToList();
                        return (candidateCol, list);
                    }
                }
                match = match.NextMatch();
            }
        }
        return null;
    }

    private Cell BindCellWithItemContext(
        Cell cell, int targetRow, object item, string collectionName, object rootData)
    {
        object? val = cell.Value;
        if (cell.Value is string str)
        {
            val = PlaceholderRegex.Replace(str, m =>
            {
                string path = m.Groups[1].Value.Trim();
                if (path.StartsWith(collectionName + ".", StringComparison.OrdinalIgnoreCase))
                {
                    string subPath = path[(collectionName.Length + 1)..];
                    var subVal = _valueResolver.Resolve(item, subPath);
                    return subVal?.ToString() ?? "";
                }
                // 否则尝试从根对象取
                var rootVal = _valueResolver.Resolve(rootData, path);
                return rootVal?.ToString() ?? "";
            });
        }

        return new Cell(targetRow, cell.Column, val, cell.RowSpan, cell.ColumnSpan, cell.Type, cell.Style);
    }

    private Cell BindCellGeneral(Cell cell, int targetRow, object rootData)
    {
        object? val = cell.Value;
        if (cell.Value is string str && str.Contains("{{"))
        {
            // 如果整个单元格仅为一个占位符如 "{{Total}}"，保留其原始数据类型（如数字、日期等）
            var singleMatch = PlaceholderRegex.Match(str.Trim());
            if (singleMatch.Success && singleMatch.Value == str.Trim())
            {
                string path = singleMatch.Groups[1].Value.Trim();
                val = _valueResolver.Resolve(rootData, path);
            }
            else
            {
                val = PlaceholderRegex.Replace(str, m =>
                {
                    string path = m.Groups[1].Value.Trim();
                    var v = _valueResolver.Resolve(rootData, path);
                    return v?.ToString() ?? "";
                });
            }
        }

        return new Cell(targetRow, cell.Column, val, cell.RowSpan, cell.ColumnSpan, cell.Type, cell.Style);
    }

    private static (object? Value, CellType Type) ExtractCellValue(IXLCell xlCell)
    {
        if (xlCell.HasFormula)
        {
            return (xlCell.FormulaA1, CellType.Formula);
        }

        if (xlCell.IsEmpty())
        {
            return (null, CellType.Blank);
        }

        return xlCell.DataType switch
        {
            XLDataType.Boolean => (xlCell.GetBoolean(), CellType.Text),
            XLDataType.Number => (xlCell.GetDouble(), CellType.Text),
            XLDataType.DateTime => (xlCell.GetDateTime(), CellType.Text),
            XLDataType.TimeSpan => (xlCell.GetTimeSpan(), CellType.Text),
            _ => (xlCell.GetString(), CellType.Text)
        };
    }

    private static CellStyle ExtractCellStyle(IXLCell xlCell)
    {
        var xlStyle = xlCell.Style;

        // 字体
        var font = new FontSpec(
            family: xlStyle.Font.FontName,
            size: (float)xlStyle.Font.FontSize,
            bold: xlStyle.Font.Bold,
            italic: xlStyle.Font.Italic,
            underline: xlStyle.Font.Underline != XLFontUnderlineValues.None,
            strikeThrough: xlStyle.Font.Strikethrough);

        // 前景色
        ColorSpec? foreColor = null;
        if (xlStyle.Font.FontColor.ColorType == XLColorType.Color)
        {
            var c = xlStyle.Font.FontColor.Color;
            foreColor = ColorSpec.FromRgba(c.R, c.G, c.B, c.A);
        }

        // 背景色
        ColorSpec? backColor = null;
        if (xlStyle.Fill.PatternType != XLFillPatternValues.None &&
            xlStyle.Fill.BackgroundColor.ColorType == XLColorType.Color)
        {
            var c = xlStyle.Fill.BackgroundColor.Color;
            if (c.A > 0)
            {
                backColor = ColorSpec.FromRgba(c.R, c.G, c.B, c.A);
            }
        }

        // 对齐
        var hAlign = xlStyle.Alignment.Horizontal switch
        {
            XLAlignmentHorizontalValues.Left => HorizontalAlignment.Left,
            XLAlignmentHorizontalValues.Center => HorizontalAlignment.Center,
            XLAlignmentHorizontalValues.Right => HorizontalAlignment.Right,
            XLAlignmentHorizontalValues.Justify => HorizontalAlignment.Justify,
            _ => HorizontalAlignment.General
        };

        var vAlign = xlStyle.Alignment.Vertical switch
        {
            XLAlignmentVerticalValues.Top => VerticalAlignment.Top,
            XLAlignmentVerticalValues.Center => VerticalAlignment.Middle,
            XLAlignmentVerticalValues.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Middle
        };

        // 边框
        var border = new BorderSpec(
            top: ConvertBorder(xlStyle.Border.TopBorder, xlStyle.Border.TopBorderColor),
            bottom: ConvertBorder(xlStyle.Border.BottomBorder, xlStyle.Border.BottomBorderColor),
            left: ConvertBorder(xlStyle.Border.LeftBorder, xlStyle.Border.LeftBorderColor),
            right: ConvertBorder(xlStyle.Border.RightBorder, xlStyle.Border.RightBorderColor));

        return new CellStyle
        {
            Font = font,
            ForeColor = foreColor,
            BackColor = backColor,
            HorizontalAlignment = hAlign,
            VerticalAlignment = vAlign,
            WrapText = xlStyle.Alignment.WrapText,
            Format = string.IsNullOrEmpty(xlStyle.NumberFormat.Format) ? null : xlStyle.NumberFormat.Format,
            Border = border
        };
    }

    private static BorderLine? ConvertBorder(XLBorderStyleValues style, XLColor color)
    {
        if (style == XLBorderStyleValues.None) return null;

        var bStyle = style switch
        {
            XLBorderStyleValues.Thin => BorderStyle.Thin,
            XLBorderStyleValues.Medium => BorderStyle.Medium,
            XLBorderStyleValues.Dashed => BorderStyle.Dashed,
            XLBorderStyleValues.Dotted => BorderStyle.Dotted,
            XLBorderStyleValues.Thick => BorderStyle.Thick,
            XLBorderStyleValues.Double => BorderStyle.Double,
            XLBorderStyleValues.Hair => BorderStyle.Hair,
            _ => BorderStyle.Thin
        };

        ColorSpec? cSpec = null;
        if (color.ColorType == XLColorType.Color)
        {
            var c = color.Color;
            cSpec = ColorSpec.FromRgba(c.R, c.G, c.B, c.A);
        }

        return new BorderLine(bStyle, cSpec);
    }
}
