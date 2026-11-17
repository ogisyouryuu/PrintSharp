using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Layout;
using PrintSharp.Styles;
using GridDocument = PrintSharp.Documents.Document;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace PrintSharp.Word;

internal sealed class WordRenderer(MainDocumentPart part, CultureInfo culture)
{
    private static readonly PaddingSpec DefaultTextPadding = new(3, 2);
    private uint _nextImageId = 1;

    public static byte[] Render(GridDocument document)
    {
        var layout = GridLayoutEngine.Instance.CalculateDocument(document);
        using var output = new MemoryStream();
        using (var package = WordprocessingDocument.Create(output, WordprocessingDocumentType.Document))
        {
            package.PackageProperties.Title = document.Metadata.Title;
            package.PackageProperties.Creator = document.Metadata.Author;
            package.PackageProperties.Subject = document.Metadata.Subject;
            var part = package.AddMainDocumentPart();
            var renderer = new WordRenderer(part, document.Culture ?? CultureInfo.InvariantCulture);
            part.Document = new WordDocument(renderer.WriteBody(layout));
        }
        return output.ToArray();
    }

    private Body WriteBody(CalculatedDocumentLayout layout)
    {
        var body = new Body();
        for (int index = 0; index < layout.PageCount; index++)
        {
            var page = layout.Pages[index];
            if (WriteTable(page) is { } table)
                body.Append(table);
            var section = WriteSection(page);
            var endOfPage = new ParagraphProperties(
                new SpacingBetweenLines { Before = "0", After = "0", Line = "1", LineRule = LineSpacingRuleValues.Exact },
                new ParagraphMarkRunProperties(new FontSize { Val = "2" }, new FontSizeComplexScript { Val = "2" }));
            if (index == layout.PageCount - 1)
                body.Append(new Paragraph(endOfPage), section);
            else
            {
                endOfPage.Append(section);
                body.Append(new Paragraph(endOfPage));
            }
        }
        if (body.ChildElements.Count == 0)
            body.Append(new Paragraph());
        return body;
    }

    private static SectionProperties WriteSection(CalculatedPageLayout page)
    {
        var settings = page.Page.Settings;
        var margins = settings.Margins;
        bool landscape = settings.Orientation == PageOrientation.Landscape;
        return new SectionProperties(
            new SectionType { Val = SectionMarkValues.NextPage },
            new PageSize
            {
                Width = (uint)Twips(page.TotalWidth > 0 ? page.TotalWidth : landscape ? 842 : 595),
                Height = (uint)Twips(page.TotalHeight > 0 ? page.TotalHeight : landscape ? 595 : 842),
                Orient = landscape ? PageOrientationValues.Landscape : PageOrientationValues.Portrait
            },
            new PageMargin
            {
                Left = (uint)Twips(margins.Left), Top = Twips(margins.Top),
                Right = (uint)Twips(margins.Right), Bottom = Twips(margins.Bottom),
                Header = 0U, Footer = 0U, Gutter = 0U
            });
    }

    private Table? WriteTable(CalculatedPageLayout layout)
    {
        var page = layout.Page;
        var settings = page.Settings;
        int rowCount = Math.Max(page.Rows.Select(row => row.Index + 1).DefaultIfEmpty().Max(),
            page.Cells.Select(cell => checked(cell.Row + cell.RowSpan)).DefaultIfEmpty().Max());
        int columnCount = Math.Max(page.Columns.Select(column => column.Index + 1).DefaultIfEmpty().Max(),
            page.Cells.Select(cell => checked(cell.Column + cell.ColumnSpan)).DefaultIfEmpty().Max());
        var rowSizes = Enumerable.Repeat(settings.DefaultRowHeight, rowCount).ToArray();
        var columnSizes = Enumerable.Repeat(settings.DefaultColumnWidth, columnCount).ToArray();
        foreach (var row in page.Rows) rowSizes[row.Index] = row.IsHidden ? 0 : row.Height;
        foreach (var column in page.Columns) columnSizes[column.Index] = column.IsHidden ? 0 : column.Width;
        var (heights, rowOffsets) = VisibleTracks(rowSizes);
        var (widths, columnOffsets) = VisibleTracks(columnSizes);
        if (heights.Count == 0 || widths.Count == 0) return null;

        var rows = Enumerable.Range(0, heights.Count).Select(_ => new Dictionary<int, Placement>()).ToArray();
        foreach (var cell in layout.Cells)
        {
            int row = rowOffsets[cell.Cell.Row];
            int column = columnOffsets[cell.Cell.Column];
            int rowSpan = rowOffsets[cell.Cell.Row + cell.Cell.RowSpan] - row;
            int columnSpan = columnOffsets[cell.Cell.Column + cell.Cell.ColumnSpan] - column;
            if (rowSpan == 0 || columnSpan == 0) continue;
            var placement = new Placement(cell, row, rowSpan, columnSpan,
                widths.GetRange(column, columnSpan).Sum());
            for (int r = row; r < row + rowSpan; r++)
                rows[r].Add(column, placement);
        }

        var table = new Table(
            new TableProperties(
                new TableWidth { Type = TableWidthUnitValues.Dxa, Width = Number(widths.Sum()) },
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableCellMarginDefault(new TopMargin { Type = TableWidthUnitValues.Dxa, Width = "0" },
                    new LeftMargin { Type = TableWidthUnitValues.Dxa, Width = "0" },
                    new BottomMargin { Type = TableWidthUnitValues.Dxa, Width = "0" },
                    new RightMargin { Type = TableWidthUnitValues.Dxa, Width = "0" })),
            new TableGrid(widths.Select(width => new GridColumn { Width = Number(width) })));
        for (int row = 0; row < heights.Count; row++)
        {
            // Word は固定行高に行内で最大の下余白を加算します (MS-OI29500 §2.1.180)。
            int bottomPadding = rows[row].Values.Select(cell => Twips(CellPadding(cell.Layout).Bottom)).DefaultIfEmpty().Max();
            int rowHeight = Math.Max(1, heights[row] - bottomPadding);
            var wordRow = new TableRow(new TableRowProperties(
                new CantSplit(), new TableRowHeight { Val = (uint)rowHeight, HeightType = HeightRuleValues.Exact }));
            int column = 0;
            while (column < widths.Count)
            {
                if (rows[row].TryGetValue(column, out var placement))
                {
                    wordRow.Append(WriteCell(placement, row));
                    column += placement.ColumnSpan;
                }
                else
                {
                    wordRow.Append(new TableCell(new TableCellProperties(CellWidth(widths[column])), NewParagraph()));
                    column++;
                }
            }
            table.Append(wordRow);
        }
        return table;
    }

    private static (List<int> Sizes, int[] Offsets) VisibleTracks(float[] sizes)
    {
        var visible = new List<int>();
        var offsets = new int[sizes.Length + 1];
        double position = 0;
        for (int i = 0; i < sizes.Length; i++)
        {
            int start = Twips(position);
            position += sizes[i];
            int size = Twips(position) - start;
            if (size > 0) visible.Add(size);
            offsets[i + 1] = visible.Count;
        }
        return (visible, offsets);
    }

    private TableCell WriteCell(Placement placement, int row)
    {
        bool firstRow = row == placement.Row;
        var layout = placement.Layout;
        var cell = layout.Cell;
        var style = layout.EffectiveStyle;
        var properties = new TableCellProperties(CellWidth(placement.Width));
        if (placement.ColumnSpan > 1) properties.Append(new GridSpan { Val = placement.ColumnSpan });
        if (placement.RowSpan > 1)
            properties.Append(new VerticalMerge { Val = firstRow ? MergedCellValues.Restart : MergedCellValues.Continue });
        if (style.Border is { } border)
        {
            var borders = new TableCellBorders();
            if (firstRow) AddBorder<TopBorder>(borders, border.Top);
            AddBorder<LeftBorder>(borders, border.Left);
            if (row == placement.Row + placement.RowSpan - 1) AddBorder<BottomBorder>(borders, border.Bottom);
            AddBorder<RightBorder>(borders, border.Right);
            if (firstRow) AddBorder<TopLeftToBottomRightCellBorder>(borders, border.Diagonal);
            if (borders.HasChildren) properties.Append(borders);
        }
        if (style.BackColor is { A: > 0 } background)
            properties.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = Color(background) });
        if (!style.WrapText) properties.Append(new NoWrap());
        var padding = CellPadding(layout);
        properties.Append(new TableCellMargin(
            new TopMargin { Type = TableWidthUnitValues.Dxa, Width = Number(Twips(padding.Top)) },
            new LeftMargin { Type = TableWidthUnitValues.Dxa, Width = Number(Twips(padding.Left)) },
            new BottomMargin { Type = TableWidthUnitValues.Dxa, Width = Number(Twips(padding.Bottom)) },
            new RightMargin { Type = TableWidthUnitValues.Dxa, Width = Number(Twips(padding.Right)) }));
        properties.Append(new TableCellVerticalAlignment
        {
            Val = style.VerticalAlignment switch
            {
                VerticalAlignment.Top => TableVerticalAlignmentValues.Top,
                VerticalAlignment.Bottom => TableVerticalAlignmentValues.Bottom,
                _ => TableVerticalAlignmentValues.Center
            }
        });

        var paragraph = NewParagraph();
        if (firstRow && cell.Type == CellType.Image && cell.Value is byte[] { Length: > 0 } bytes &&
            layout.ContentBounds.Width > 0 && layout.ContentBounds.Height > 0)
            paragraph.Append(new Run(WriteImage(bytes, layout.ContentBounds)));
        else if (firstRow && cell.Type is not (CellType.Blank or CellType.Image))
            WriteText(paragraph, cell, style);
        return new TableCell(properties, paragraph);
    }

    private void WriteText(Paragraph paragraph, Cell cell, CellStyle style)
    {
        object? value = cell.Type == CellType.Formula ? (cell.Value as FormulaValue)?.Value : cell.Value;
        if (value is null) return;
        string? text = value is IFormattable formatted && !string.IsNullOrWhiteSpace(style.Format)
            ? formatted.ToString(style.Format, culture)
            : Convert.ToString(value, culture);
        if (string.IsNullOrEmpty(text)) return;

        paragraph.ParagraphProperties!.Append(new Justification
        {
            Val = style.HorizontalAlignment switch
            {
                HorizontalAlignment.Center => JustificationValues.Center,
                HorizontalAlignment.Right => JustificationValues.Right,
                HorizontalAlignment.Justify => JustificationValues.Both,
                HorizontalAlignment.General when value is DateTime or DateOnly or byte or sbyte or
                    short or ushort or int or uint or long or ulong or float or double or decimal => JustificationValues.Right,
                _ => JustificationValues.Left
            }
        });
        var font = style.Font ?? FontSpec.Default;
        var runProperties = new RunProperties();
        if (!string.IsNullOrWhiteSpace(font.Family))
            runProperties.Append(new RunFonts { Ascii = font.Family, HighAnsi = font.Family, EastAsia = font.Family, ComplexScript = font.Family });
        if (font.Bold) runProperties.Append(new Bold(), new BoldComplexScript());
        if (font.Italic) runProperties.Append(new Italic(), new ItalicComplexScript());
        if (font.StrikeThrough) runProperties.Append(new Strike());
        if (style.ForeColor is { } color) runProperties.Append(new DocumentFormat.OpenXml.Wordprocessing.Color { Val = Color(color) });
        string fontSize = Number(Math.Clamp((int)Math.Round(font.Size * 2), 2, 1638));
        runProperties.Append(new FontSize { Val = fontSize }, new FontSizeComplexScript { Val = fontSize });
        if (font.Underline) runProperties.Append(new Underline { Val = UnderlineValues.Single });
        var run = new Run(runProperties);
        var lines = text.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) run.Append(new Break());
            var pieces = lines[i].Split('\t');
            for (int j = 0; j < pieces.Length; j++)
            {
                if (j > 0) run.Append(new TabChar());
                run.Append(new Text(string.Concat(pieces[j].EnumerateRunes()
                    .Where(rune => rune.Value is >= 0x20 and not (0xfffe or 0xffff))
                    .Select(rune => rune.ToString()))) { Space = SpaceProcessingModeValues.Preserve });
            }
        }
        paragraph.Append(run);
    }

    private Drawing WriteImage(byte[] bytes, LayoutRect bounds)
    {
        ReadOnlySpan<byte> signature = bytes;
        var type = signature.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? ImagePartType.Png
            : signature.StartsWith(new byte[] { 255, 216, 255 }) ? ImagePartType.Jpeg
            : signature.StartsWith("GIF87a"u8) || signature.StartsWith("GIF89a"u8) ? ImagePartType.Gif
            : signature.StartsWith("BM"u8) ? ImagePartType.Bmp
            : throw new InvalidDataException("Word cell images must be PNG, JPEG, GIF, or BMP.");
        var image = part.AddImagePart(type);
        using var source = new MemoryStream(bytes, writable: false);
        image.FeedData(source);

        const string ns = "http://schemas.openxmlformats.org/";
        XNamespace w = ns + "wordprocessingml/2006/main";
        XNamespace wp = ns + "drawingml/2006/wordprocessingDrawing";
        XNamespace a = ns + "drawingml/2006/main";
        XNamespace pic = ns + "drawingml/2006/picture";
        XNamespace r = ns + "officeDocument/2006/relationships";
        long width = Math.Max(1L, (long)Math.Round(bounds.Width * 12700d));
        long height = Math.Max(1L, (long)Math.Round(bounds.Height * 12700d));
        var xml = new XElement(w + "drawing",
            new XElement(wp + "inline",
                new XElement(wp + "extent", new XAttribute("cx", width), new XAttribute("cy", height)),
                new XElement(wp + "docPr", new XAttribute("id", _nextImageId++), new XAttribute("name", "Cell image")),
                new XElement(a + "graphic", new XElement(a + "graphicData", new XAttribute("uri", pic.NamespaceName),
                    new XElement(pic + "pic",
                        new XElement(pic + "nvPicPr",
                            new XElement(pic + "cNvPr", new XAttribute("id", 0), new XAttribute("name", "Cell image")),
                            new XElement(pic + "cNvPicPr")),
                        new XElement(pic + "blipFill", new XElement(a + "blip", new XAttribute(r + "embed", part.GetIdOfPart(image))),
                            new XElement(a + "stretch", new XElement(a + "fillRect"))),
                        new XElement(pic + "spPr",
                            new XElement(a + "xfrm",
                                new XElement(a + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
                                new XElement(a + "ext", new XAttribute("cx", width), new XAttribute("cy", height))),
                            new XElement(a + "prstGeom", new XAttribute("prst", "rect"), new XElement(a + "avLst"))))))));
        return new Drawing(xml.ToString(SaveOptions.DisableFormatting));
    }

    private static void AddBorder<T>(TableCellBorders borders, BorderLine? border) where T : BorderType, new()
    {
        if (border is null) return;
        float width = border.Width > 0 ? border.Width : border.Style switch
        {
            BorderStyle.Medium => 1.5f, BorderStyle.Thick => 2.5f, BorderStyle.Hair => 0.25f, _ => 0.75f
        };
        borders.Append(new T
        {
            Val = border.Style switch
            {
                BorderStyle.None => BorderValues.Nil,
                BorderStyle.Dashed => BorderValues.Dashed,
                BorderStyle.Dotted => BorderValues.Dotted,
                BorderStyle.Double => BorderValues.Double,
                _ => BorderValues.Single
            },
            Color = Color(border.Color ?? ColorSpec.Black),
            Size = (uint)Math.Clamp((int)Math.Round(width * 8), 2, 96)
        });
    }

    private static Paragraph NewParagraph() => new(new ParagraphProperties(new SpacingBetweenLines { Before = "0", After = "0" }));
    private static PaddingSpec CellPadding(CalculatedCellLayout cell) =>
        cell.EffectiveStyle.Padding ?? (cell.Cell.Type == CellType.Image ? PaddingSpec.Zero : DefaultTextPadding);
    private static TableCellWidth CellWidth(int width) => new() { Type = TableWidthUnitValues.Dxa, Width = Number(width) };
    private static string Color(ColorSpec color) => $"{color.R:X2}{color.G:X2}{color.B:X2}";
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static int Twips(double points) => checked((int)Math.Round(points * 20));
    private sealed record Placement(CalculatedCellLayout Layout, int Row, int RowSpan, int ColumnSpan, int Width);
}
