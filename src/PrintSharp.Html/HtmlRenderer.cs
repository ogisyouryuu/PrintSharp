using System.Globalization;
using System.Net;
using System.Text;
using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Layout;
using PrintSharp.Styles;

namespace PrintSharp.Html;

internal static class HtmlRenderer
{
    private static readonly PaddingSpec DefaultTextPadding = new(3f, 2f);

    public static string Render(Document document)
    {
        var layout = GridLayoutEngine.Instance.CalculateDocument(document);
        var culture = document.Culture ?? CultureInfo.InvariantCulture;
        var html = new StringBuilder();
        html.AppendLine("<!doctype html>").Append("<html");
        if (culture.Name.Length > 0)
            html.Append(" lang=\"").Append(WebUtility.HtmlEncode(culture.Name)).Append('"');
        html.AppendLine("><head>")
            .AppendLine("<meta charset=\"utf-8\">")
            .Append("<title>").Append(WebUtility.HtmlEncode(document.Metadata.Title ?? "PrintSharp")).AppendLine("</title>")
            .AppendLine("""
                <style>
                body{margin:0;color:#000;background:#eee}
                .page{position:relative;overflow:hidden;background:#fff}
                .cell,.content{position:absolute;overflow:hidden;box-sizing:border-box}
                .content{display:flex;flex-direction:column}
                .content img{display:block;width:100%;height:100%;object-fit:contain}
                @media screen{.page{margin:12pt auto}}
                @media print{
                  body{background:#fff;print-color-adjust:exact}
                  .page+.page{break-before:page}
                }
                """);
        for (int i = 0; i < layout.PageCount; i++)
        {
            var page = layout.Pages[i];
            html.Append(CultureInfo.InvariantCulture, $"@page sheet{i}{{size:{Points(page.TotalWidth)} {Points(page.TotalHeight)};margin:0}}")
                .AppendLine();
        }
        html.AppendLine("</style></head><body>");
        for (int i = 0; i < layout.PageCount; i++)
        {
            var page = layout.Pages[i];
            html.Append(CultureInfo.InvariantCulture, $"<section class=\"page\" style=\"page:sheet{i};width:{Points(page.TotalWidth)};height:{Points(page.TotalHeight)}\">")
                .AppendLine();
            foreach (var cell in page.Cells)
                WriteCell(html, cell, culture);
            html.AppendLine("</section>");
        }
        return html.AppendLine("</body></html>").ToString();
    }

    private static void WriteCell(StringBuilder html, CalculatedCellLayout layout, CultureInfo culture)
    {
        var bounds = layout.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return;

        var style = layout.EffectiveStyle;
        html.Append("<div class=\"cell\" style=\"").Append(Position(bounds));
        if (style.BackColor is { } background)
            html.Append("background:").Append(Color(background)).Append(';');
        if (style.Border is { } border)
        {
            WriteBorder(html, "top", border.Top);
            WriteBorder(html, "right", border.Right);
            WriteBorder(html, "bottom", border.Bottom);
            WriteBorder(html, "left", border.Left);
        }
        html.Append("\">");

        // 内容の位置は外枠と同じ座標系に置き、CSS の境界線の太さに依存させません。
        html.Append("</div>");
        var content = layout.ContentBounds;
        var cell = layout.Cell;
        // PDF と同じ既定の文字余白を、書式で余白が指定されていない場合に適用します。
        if (cell.Type != CellType.Image && style.Padding is null)
        {
            var padding = DefaultTextPadding;
            content = new LayoutRect(content.X + padding.Left, content.Y + padding.Top,
                content.Width - padding.Horizontal, content.Height - padding.Vertical);
        }
        if (cell.Type == CellType.Blank || cell.Value is null || content.Width <= 0 || content.Height <= 0)
        {
            html.AppendLine();
            return;
        }

        html.Append("<div class=\"content\" style=\"").Append(Position(content));
        if (cell.Type == CellType.Image)
        {
            html.Append("\">");
            if (cell.Value is byte[] bytes && ImageMediaType(bytes) is { } mediaType)
                html.Append("<img alt=\"\" src=\"data:").Append(mediaType).Append(";base64,")
                    .Append(Convert.ToBase64String(bytes)).Append("\">");
        }
        else
        {
            object? value = cell.Value is FormulaValue formula ? formula.Value : cell.Value;
            string? text = value is IFormattable formatted && !string.IsNullOrWhiteSpace(style.Format)
                ? formatted.ToString(style.Format, culture)
                : Convert.ToString(value, culture);
            WriteTextStyle(html, style, value);
            html.Append("\"><span>").Append(WebUtility.HtmlEncode(text)).Append("</span>");
        }
        html.AppendLine("</div>");
    }

    private static void WriteTextStyle(StringBuilder html, CellStyle style, object? value)
    {
        var font = style.Font ?? FontSpec.Default;
        if (!string.IsNullOrWhiteSpace(font.Family))
        {
            html.Append("font-family:'");
            foreach (var rune in font.Family.EnumerateRunes())
                html.Append(CultureInfo.InvariantCulture, $"\\{rune.Value:X} ");
            html.Append("',sans-serif;");
        }
        else
        {
            html.Append("font-family:sans-serif;");
        }

        html.Append("font-size:").Append(Points(font.Size)).Append(';');
        if (font.Bold) html.Append("font-weight:bold;");
        if (font.Italic) html.Append("font-style:italic;");
        if (font.Underline || font.StrikeThrough)
        {
            html.Append("text-decoration:");
            if (font.Underline) html.Append("underline ");
            if (font.StrikeThrough) html.Append("line-through");
            html.Append(';');
        }
        if (style.ForeColor is { } foreground)
            html.Append("color:").Append(Color(foreground)).Append(';');

        var horizontal = style.HorizontalAlignment switch
        {
            HorizontalAlignment.Center => "center",
            HorizontalAlignment.Right => "right",
            HorizontalAlignment.Justify => "justify",
            HorizontalAlignment.General when value is DateTime or DateOnly or
                byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => "right",
            _ => "left"
        };
        var vertical = style.VerticalAlignment switch
        {
            VerticalAlignment.Top => "flex-start",
            VerticalAlignment.Bottom => "flex-end",
            _ => "center"
        };
        html.Append("text-align:").Append(horizontal).Append(";justify-content:").Append(vertical)
            .Append(style.WrapText ? ";white-space:pre-wrap;overflow-wrap:anywhere;" : ";white-space:pre;");
    }

    private static void WriteBorder(StringBuilder html, string side, BorderLine? border)
    {
        if (border is null || border.Style == BorderStyle.None) return;
        var cssStyle = border.Style switch
        {
            BorderStyle.Dashed => "dashed",
            BorderStyle.Dotted => "dotted",
            BorderStyle.Double => "double",
            _ => "solid"
        };
        float width = border.Width > 0 ? border.Width : border.Style switch
        {
            BorderStyle.Medium => 1.5f,
            BorderStyle.Thick => 2.5f,
            BorderStyle.Double => 2.25f,
            BorderStyle.Hair => 0.25f,
            _ => 0.75f
        };
        html.Append("border-").Append(side).Append(':').Append(Points(width)).Append(' ')
            .Append(cssStyle).Append(' ').Append(Color(border.Color ?? ColorSpec.Black)).Append(';');
    }

    private static string? ImageMediaType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return "image/png";
        if (bytes.StartsWith(new byte[] { 255, 216, 255 })) return "image/jpeg";
        if (bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8)) return "image/gif";
        if (bytes.Length >= 12 && bytes.StartsWith("RIFF"u8) && bytes[8..].StartsWith("WEBP"u8)) return "image/webp";
        if (bytes.StartsWith("BM"u8)) return "image/bmp";
        return null;
    }

    private static string Position(LayoutRect rect) =>
        $"left:{Points(rect.X)};top:{Points(rect.Y)};width:{Points(rect.Width)};height:{Points(rect.Height)};";

    private static string Points(float value) => value.ToString("0.####", CultureInfo.InvariantCulture) + "pt";

    private static string Color(ColorSpec color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}{color.A:X2}";
}
