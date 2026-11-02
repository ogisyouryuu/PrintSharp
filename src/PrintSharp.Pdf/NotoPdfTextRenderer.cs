using PrintSharp.Layout;
using PrintSharp.Pdf.Preview;
using PrintSharp.Styles;
using SkiaSharp;
using Topten.RichTextKit;
using Topten.RichTextKit.Utils;

namespace PrintSharp.Pdf;

/// <summary>同梱 Noto フォントと HarfBuzz を使用してセル内に多言語を描画します。</summary>
public sealed class NotoPdfTextRenderer(NotoFontRegistry fonts) : IDisplayListTextRenderer
{
    /// <inheritdoc/>
    public void DrawText(SKCanvas canvas, string text, CellStyle style, LayoutRect content, float scale)
    {
        var builder = new DisplayListBuilder(new(), new());
        AppendText(builder, text, style, content, scale);
        var page = new DisplayPage(1, content.Right, content.Bottom, builder.Commands.AsReadOnly());
        new PreviewDocument([page], builder.Resources, SKDocumentPdfMetadata.Default).DrawPage(canvas, page);
    }

    /// <inheritdoc/>
    public void AppendText(DisplayListBuilder builder, string text, CellStyle style, LayoutRect content, float scale)
    {
        var font = style.Font ?? FontSpec.Default;
        var color = style.ForeColor ?? ColorSpec.Black;
        var block = new TextBlock
        {
            FontMapper = fonts,
            MaxWidth = style.WrapText ? content.Width : null,
            RenderWidth = content.Width,
            Alignment = style.HorizontalAlignment switch
            {
                HorizontalAlignment.Center => TextAlignment.Center,
                HorizontalAlignment.Right => TextAlignment.Right,
                HorizontalAlignment.Left or HorizontalAlignment.Justify => TextAlignment.Left,
                _ => TextAlignment.Auto
            }
        };
        foreach (var run in fonts.ResolveRuns(text, font.Family, font.Bold))
            block.AddText(run.Text, new Style
            {
                FontFamily = run.Family,
                FontSize = Math.Max(1, font.Size * scale),
                FontWeight = font.Bold ? 700 : 400,
                FontItalic = font.Italic,
                TextColor = new SKColor(color.R, color.G, color.B, color.A),
                Underline = font.Underline ? UnderlineStyle.Solid : UnderlineStyle.None,
                StrikeThrough = font.StrikeThrough ? StrikeThroughStyle.Solid : StrikeThroughStyle.None
            });
        float offset = style.VerticalAlignment switch
        {
            VerticalAlignment.Bottom => content.Height - block.MeasuredHeight,
            VerticalAlignment.Middle => (content.Height - block.MeasuredHeight) / 2,
            _ => 0
        };
        if (style.WrapText && style.HorizontalAlignment == HorizontalAlignment.Justify)
            JustifyLines(block, content.Width);
        builder.Add(new("save"));
        builder.Add(new("clipRect") { Values = [content.X, content.Y, content.Width, content.Height] });
        foreach (var run in block.FontRuns.Where(r => r.RunKind != FontRunKind.TrailingWhitespace && r.Glyphs.Length > 0))
        {
            var positions = run.GlyphPositions.ToArray();
            for (int i = 0; i < positions.Length; i++)
                positions[i] = new(positions[i].X + content.X, positions[i].Y + content.Y + Math.Max(0, offset));
            builder.DrawGlyphRun(run.Typeface, run.Style.FontSize, font.Italic ? -0.2f : 0,
                run.Glyphs.ToArray(), positions, run.Style.TextColor);
            using var skFont = new SKFont(run.Typeface, run.Style.FontSize);
            float baseline = content.Y + Math.Max(0, offset) + run.Line.YCoord + run.Line.BaseLine;
            using var paint = new SKPaint { Color = run.Style.TextColor };
            if (run.Style.Underline != UnderlineStyle.None)
            {
                paint.StrokeWidth = Math.Max(1, skFont.Metrics.UnderlineThickness ?? 1);
                float y = baseline + Math.Max(1, skFont.Metrics.UnderlinePosition ?? 1);
                builder.DrawLine(content.X + run.XCoord, y, content.X + run.XCoord + run.Width, y, paint);
            }
            if (run.Style.StrikeThrough != StrikeThroughStyle.None)
            {
                paint.StrokeWidth = Math.Max(1, skFont.Metrics.StrikeoutThickness ?? 1);
                float y = baseline + (skFont.Metrics.StrikeoutPosition ?? -run.Style.FontSize * 0.3f);
                builder.DrawLine(content.X + run.XCoord, y, content.X + run.XCoord + run.Width, y, paint);
            }
        }
        builder.Add(new("restore"));
    }

    private static void JustifyLines(TextBlock block, float width)
    {
        foreach (var line in block.Lines.Take(block.Lines.Count - 1))
        {
            // 明示的な改行と段落末尾は引き伸ばしません。
            if (line.Runs.Any(r => r.CodePoints.ToArray().Any(cp => cp is 10 or 13 or 0x2028 or 0x2029))) continue;
            var gaps = new List<float>();
            foreach (var run in line.Runs.Where(r => r.RunKind == FontRunKind.Normal))
            {
                var points = run.CodePoints;
                for (int i = 0; i < points.Length; i++)
                    if (points[i] == ' ')
                        gaps.Add(Math.Max(run.GetXCoordOfCodePointIndex(run.Start + i), run.GetXCoordOfCodePointIndex(run.Start + i + 1)));
            }
            if (gaps.Count == 0 || line.Width >= width) continue;
            float extra = (width - line.Width) / gaps.Count;
            float Shift(float x) => gaps.Count(gap => gap <= x + 0.0001f) * extra;
            foreach (var run in line.Runs)
            {
                // 組版キャッシュや別セルと共有する可能性のある配列は変更しません。
                var positions = run.GlyphPositions.ToArray();
                for (int i = 0; i < positions.Length; i++) positions[i].X += Shift(positions[i].X);
                run.GlyphPositions = new Slice<SKPoint>(positions);
                float x = run.XCoord;
                float shift = Shift(x);
                run.XCoord += shift;
                run.Width += Shift(x + run.Width) - shift;
            }
        }
    }

}
