using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Layout;
using PrintSharp.Layout.Fonts;
using PrintSharp.Styles;
using CoreHorizontalAlignment = PrintSharp.Styles.HorizontalAlignment;
using CoreBorderStyle = PrintSharp.Styles.BorderStyle;

namespace PrintSharp.Windows;

/// <summary>将 PrintSharp 网格文档渲染到 Windows <see cref="PrintDocument"/>。</summary>
public sealed class PrintDocumentRenderer
{
    /// <summary>默认渲染器实例。</summary>
    public static PrintDocumentRenderer Instance { get; } = new();

    /// <summary>创建配置完成的打印文档；调用方负责释放返回对象。</summary>
    public PrintDocument RenderToPrintDocument(Document document, PrintDocumentRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= PrintDocumentRenderOptions.Default;
        if (!float.IsFinite(options.PointsPerLogicalUnit) || options.PointsPerLogicalUnit <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "PointsPerLogicalUnit must be a finite positive number.");

        var layout = GridLayoutEngine.Instance.CalculateDocument(document);
        var printDocument = new PrintDocument
        {
            DocumentName = string.IsNullOrWhiteSpace(document.Metadata.Title) ? "PrintSharp Document" : document.Metadata.Title
        };
        printDocument.DefaultPageSettings.Landscape = layout.DefaultPage.Page.Settings.Orientation == PageOrientation.Landscape;

        int pageIndex = 0;
        printDocument.BeginPrint += (_, _) => pageIndex = 0;
        printDocument.PrintPage += (_, e) =>
        {
            var pageLayout = layout.Pages[pageIndex];
            e.PageSettings.Landscape = pageLayout.Page.Settings.Orientation == PageOrientation.Landscape;
            var graphics = e.Graphics!;
            var originalUnit = graphics.PageUnit;
            graphics.PageUnit = GraphicsUnit.Point;
            try
            {
                foreach (var cell in pageLayout.Cells)
                    DrawCell(graphics, cell, options);
            }
            finally
            {
                graphics.PageUnit = originalUnit;
            }

            pageIndex++;
            e.HasMorePages = pageIndex < layout.Pages.Count;
        };
        return printDocument;
    }

    /// <summary>创建并发送到默认打印机的打印任务。</summary>
    public void Print(Document document, PrintDocumentRenderOptions? options = null)
    {
        using var printDocument = RenderToPrintDocument(document, options);
        printDocument.Print();
    }

    private static void DrawCell(Graphics graphics, CalculatedCellLayout layout, PrintDocumentRenderOptions options)
    {
        var cell = layout.Cell;
        var style = layout.EffectiveStyle;
        var bounds = Scale(layout.Bounds, options.PointsPerLogicalUnit);
        var content = Scale(layout.ContentBounds, options.PointsPerLogicalUnit);

        if (style.BackColor is { A: > 0 } background)
        using (var brush = new SolidBrush(ToColor(background)))
            graphics.FillRectangle(brush, bounds);
        DrawBorders(graphics, bounds, style.Border, options.PointsPerLogicalUnit);

        if (cell.Type == CellType.Image && cell.Value is byte[] imageBytes && imageBytes.Length > 0)
        {
            using var stream = new MemoryStream(imageBytes, writable: false);
            using var image = Image.FromStream(stream);
            if (content.Width > 0 && content.Height > 0)
                graphics.DrawImage(image, content);
            return;
        }
        if (cell.Type is CellType.Blank or CellType.Image || cell.Value is null) return;

        string text = Convert.ToString(cell.Value, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
        if (cell.Type == CellType.Formula && text.StartsWith('=')) text = text[1..];
        if (text.Length == 0 || content.Width <= 0 || content.Height <= 0) return;

        var fontSpec = style.Font ?? FontSpec.Default;
        string family = (options.FontResolver ?? DefaultFontResolver.Instance).Resolve(fontSpec).ResolvedFamilyName;
        var fontStyle = FontStyle.Regular;
        if (fontSpec.Bold) fontStyle |= FontStyle.Bold;
        if (fontSpec.Italic) fontStyle |= FontStyle.Italic;
        if (fontSpec.Underline) fontStyle |= FontStyle.Underline;
        if (fontSpec.StrikeThrough) fontStyle |= FontStyle.Strikeout;
        using var font = new Font(family, Math.Max(1f, fontSpec.Size * options.PointsPerLogicalUnit), fontStyle, GraphicsUnit.Point);
        using var brushText = new SolidBrush(style.ForeColor is { } foreColor ? ToColor(foreColor) : Color.Black);
        using var format = new StringFormat
        {
            Alignment = style.HorizontalAlignment switch
            {
                CoreHorizontalAlignment.Center => StringAlignment.Center,
                CoreHorizontalAlignment.Right => StringAlignment.Far,
                _ => StringAlignment.Near
            },
            LineAlignment = style.VerticalAlignment switch
            {
                VerticalAlignment.Top => StringAlignment.Near,
                VerticalAlignment.Bottom => StringAlignment.Far,
                _ => StringAlignment.Center
            },
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = style.WrapText ? StringFormatFlags.LineLimit : StringFormatFlags.NoWrap
        };
        graphics.DrawString(text, font, brushText, content, format);
    }

    private static RectangleF Scale(LayoutRect rect, float scale) =>
        new(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);

    private static Color ToColor(ColorSpec color) => Color.FromArgb(color.A, color.R, color.G, color.B);

    private static void DrawBorders(Graphics graphics, RectangleF rect, BorderSpec? border, float scale)
    {
        if (border is null) return;
        DrawBorder(graphics, rect.Left, rect.Top, rect.Right, rect.Top, border.Top, scale);
        DrawBorder(graphics, rect.Left, rect.Bottom, rect.Right, rect.Bottom, border.Bottom, scale);
        DrawBorder(graphics, rect.Left, rect.Top, rect.Left, rect.Bottom, border.Left, scale);
        DrawBorder(graphics, rect.Right, rect.Top, rect.Right, rect.Bottom, border.Right, scale);
        DrawBorder(graphics, rect.Left, rect.Top, rect.Right, rect.Bottom, border.Diagonal, scale);
    }

    private static void DrawBorder(Graphics graphics, float x1, float y1, float x2, float y2, BorderLine? line, float scale)
    {
        if (line is null || line.Style == CoreBorderStyle.None) return;
        float width = line.Width > 0 ? line.Width * scale : line.Style switch
        {
            CoreBorderStyle.Medium => 1.5f,
            CoreBorderStyle.Thick => 2.5f,
            CoreBorderStyle.Hair => 0.25f,
            _ => 0.75f
        };
        var dashStyle = line.Style switch
        {
            CoreBorderStyle.Dashed => DashStyle.Dash,
            CoreBorderStyle.Dotted => DashStyle.Dot,
            _ => DashStyle.Solid
        };
        using var pen = new Pen(line.Color is { } color ? ToColor(color) : Color.Black, width) { DashStyle = dashStyle };
        graphics.DrawLine(pen, x1, y1, x2, y2);
        if (line.Style == CoreBorderStyle.Double)
        {
            const float inset = 1.5f;
            if (Math.Abs(y2 - y1) < 0.001f)
                graphics.DrawLine(pen, x1, y1 + inset, x2, y2 + inset);
            else if (Math.Abs(x2 - x1) < 0.001f)
                graphics.DrawLine(pen, x1 + inset, y1, x2 + inset, y2);
        }
    }
}


