using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Layout;
using PrintSharp.Layout.Fonts;
using PrintSharp.Styles;

namespace PrintSharp.Pdf;

/// <summary>将 PrintSharp 网格布局渲染为 PDFsharp 文档或 PDF 文件。</summary>
public sealed class PdfSharpRenderer
{
    /// <summary>默认渲染器单例。</summary>
    public static PdfSharpRenderer Instance { get; } = new();

    /// <summary>渲染为 PDFsharp 文档；调用方负责释放返回值。</summary>
    public PdfDocument RenderToDocument(Document document, PdfRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        options ??= PdfRenderOptions.Default;
        if (!float.IsFinite(options.PointsPerLogicalUnit) || options.PointsPerLogicalUnit <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "PointsPerLogicalUnit must be a finite positive number.");

        var layout = GridLayoutEngine.Instance.CalculateDocument(document);
        var pdf = new PdfDocument();
        if (!string.IsNullOrWhiteSpace(document.Metadata.Title)) pdf.Info.Title = document.Metadata.Title;
        if (!string.IsNullOrWhiteSpace(document.Metadata.Author)) pdf.Info.Author = document.Metadata.Author;
        if (!string.IsNullOrWhiteSpace(document.Metadata.Subject)) pdf.Info.Subject = document.Metadata.Subject;
        if (!string.IsNullOrWhiteSpace(document.Metadata.Keywords)) pdf.Info.Keywords = document.Metadata.Keywords;

        foreach (var pageLayout in layout.Pages)
        {
            var page = pdf.AddPage();
            page.Orientation = pageLayout.Page.Settings.Orientation == PageOrientation.Landscape
                ? PdfSharp.PageOrientation.Landscape : PdfSharp.PageOrientation.Portrait;
            float width = pageLayout.TotalWidth * options.PointsPerLogicalUnit;
            float height = pageLayout.TotalHeight * options.PointsPerLogicalUnit;
            if (width > 0) page.Width = XUnit.FromPoint(width);
            if (height > 0) page.Height = XUnit.FromPoint(height);

            using var graphics = XGraphics.FromPdfPage(page);
            foreach (var cellLayout in pageLayout.Cells)
                DrawCell(graphics, cellLayout, options);
        }
        return pdf;
    }

    /// <summary>渲染并写入目标数据流（不会关闭目标流）。</summary>
    public void Render(Document document, Stream outputStream, PdfRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(outputStream);
        using var pdf = RenderToDocument(document, options);
        pdf.Save(outputStream, closeStream: false);
    }

    /// <summary>渲染并保存到指定文件。</summary>
    public void Render(Document document, string filePath, PdfRenderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var pdf = RenderToDocument(document, options);
        pdf.Save(filePath);
    }

    /// <summary>渲染为 PDF 二进制字节数组。</summary>
    public byte[] RenderToBytes(Document document, PdfRenderOptions? options = null)
    {
        using var stream = new MemoryStream();
        Render(document, stream, options);
        return stream.ToArray();
    }

    private static void DrawCell(XGraphics gfx, CalculatedCellLayout layout, PdfRenderOptions options)
    {
        var cell = layout.Cell;
        var style = layout.EffectiveStyle;
        var bounds = Scale(layout.Bounds, options.PointsPerLogicalUnit);
        var content = Scale(layout.ContentBounds, options.PointsPerLogicalUnit);

        if (style.BackColor is { A: > 0 } background)
            gfx.DrawRectangle(ToBrush(background), bounds.X, bounds.Y, bounds.Width, bounds.Height);
        DrawBorders(gfx, bounds, style.Border, options.PointsPerLogicalUnit);

        if (cell.Type == CellType.Image && cell.Value is byte[] imageBytes && imageBytes.Length > 0)
        {
            using var imageStream = new MemoryStream(imageBytes, writable: false);
            using var image = XImage.FromStream(imageStream);
            if (content.Width > 0 && content.Height > 0)
                gfx.DrawImage(image, content.X, content.Y, content.Width, content.Height);
            return;
        }
        if (cell.Type is CellType.Blank or CellType.Image || cell.Value is null) return;

        string text = Convert.ToString(cell.Value, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
        if (cell.Type == CellType.Formula && text.StartsWith('=')) text = text[1..];
        if (text.Length == 0 || content.Width <= 0 || content.Height <= 0) return;

        var fontSpec = style.Font ?? FontSpec.Default;
        var resolved = (options.FontResolver ?? DefaultFontResolver.Instance).Resolve(fontSpec);
        var fontStyle = XFontStyleEx.Regular;
        if (fontSpec.Bold) fontStyle |= XFontStyleEx.Bold;
        if (fontSpec.Italic) fontStyle |= XFontStyleEx.Italic;
        var font = new XFont(resolved.ResolvedFamilyName, Math.Max(1, fontSpec.Size * options.PointsPerLogicalUnit), fontStyle);
        var brush = style.ForeColor is { } color ? ToBrush(color) : XBrushes.Black;

        if (style.WrapText)
        {
            var formatter = new XTextFormatter(gfx) { Alignment = ToParagraphAlignment(style.HorizontalAlignment) };
            formatter.DrawString(text, font, brush, new XRect(content.X, content.Y, content.Width, content.Height), XStringFormats.TopLeft);
        }
        else
        {
            var format = new XStringFormat
            {
                Alignment = ToStringAlignment(style.HorizontalAlignment),
                LineAlignment = style.VerticalAlignment switch
                {
                    VerticalAlignment.Top => XLineAlignment.Near,
                    VerticalAlignment.Bottom => XLineAlignment.Far,
                    _ => XLineAlignment.Center
                },
            };
            gfx.DrawString(text, font, brush, new XRect(content.X, content.Y, content.Width, content.Height), format);
        }
    }

    private static LayoutRect Scale(LayoutRect rect, float scale) => new(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);

    private static XBrush ToBrush(ColorSpec color) => new XSolidBrush(XColor.FromArgb(color.A, color.R, color.G, color.B));

    private static XStringAlignment ToStringAlignment(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Center => XStringAlignment.Center,
        HorizontalAlignment.Right => XStringAlignment.Far,
        _ => XStringAlignment.Near
    };

    private static XParagraphAlignment ToParagraphAlignment(HorizontalAlignment alignment) => alignment switch
    {
        HorizontalAlignment.Center => XParagraphAlignment.Center,
        HorizontalAlignment.Right => XParagraphAlignment.Right,
        HorizontalAlignment.Justify => XParagraphAlignment.Justify,
        _ => XParagraphAlignment.Left
    };

    private static void DrawBorders(XGraphics gfx, LayoutRect rect, BorderSpec? border, float scale)
    {
        if (border is null) return;
        DrawBorder(gfx, rect.X, rect.Y, rect.Right, rect.Y, border.Top, scale);
        DrawBorder(gfx, rect.X, rect.Bottom, rect.Right, rect.Bottom, border.Bottom, scale);
        DrawBorder(gfx, rect.X, rect.Y, rect.X, rect.Bottom, border.Left, scale);
        DrawBorder(gfx, rect.Right, rect.Y, rect.Right, rect.Bottom, border.Right, scale);
        DrawBorder(gfx, rect.X, rect.Y, rect.Right, rect.Bottom, border.Diagonal, scale);
    }

    private static void DrawBorder(XGraphics gfx, double x1, double y1, double x2, double y2, BorderLine? line, float scale)
    {
        if (line is null || line.Style == BorderStyle.None) return;
        double width = line.Width > 0 ? line.Width * scale : line.Style switch
        {
            BorderStyle.Medium => 1.5, BorderStyle.Thick => 2.5, BorderStyle.Hair => 0.25, _ => 0.75
        };
        var pen = new XPen(line.Color is { } color ? XColor.FromArgb(color.A, color.R, color.G, color.B) : XColors.Black, width);
        pen.DashStyle = line.Style switch
        {
            BorderStyle.Dashed => XDashStyle.Dash,
            BorderStyle.Dotted => XDashStyle.Dot,
            _ => XDashStyle.Solid
        };
        // rect 坐标已经由布局映射转换成 PDF 点；线宽仍需按同一比例转换。
        gfx.DrawLine(pen, x1, y1, x2, y2);
        if (line.Style == BorderStyle.Double)
        {
            // 双线用靠内的平行线表示；间隔固定为 1.5 PDF 点。
            const double inset = 1.5;
            bool horizontal = Math.Abs(y2 - y1) < 0.0001;
            if (horizontal)
            {
                gfx.DrawLine(pen, x1, y1 + inset, x2, y2 + inset);
            }
            else
            {
                gfx.DrawLine(pen, x1 + inset, y1, x2 + inset, y2);
            }
        }
    }
}
