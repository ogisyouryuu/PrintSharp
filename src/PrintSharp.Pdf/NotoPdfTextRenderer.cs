using PrintSharp.Layout;
using PrintSharp.Styles;
using SkiaSharp;
using Topten.RichTextKit;
using Topten.RichTextKit.Utils;

namespace PrintSharp.Pdf;

/// <summary>同梱 Noto フォントと HarfBuzz を使用してセル内に多言語を描画します。</summary>
public sealed class NotoPdfTextRenderer(NotoFontRegistry fonts) : IPdfTextRenderer
{
    /// <inheritdoc/>
    public void DrawText(SKCanvas canvas, string text, CellStyle style, LayoutRect content, float scale)
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
        canvas.Save();
        try
        {
            // 固定行高の帳票では隣接セルへのはみ出しを防止します。
            canvas.ClipRect(new SKRect(content.X, content.Y, content.Right, content.Bottom));
            canvas.Translate(content.X, content.Y + Math.Max(0, offset));
            if (font.Italic) PaintItalic(canvas, block);
            else block.Paint(canvas);
        }
        finally { canvas.Restore(); }
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

    private static void PaintItalic(SKCanvas canvas, TextBlock block)
    {
        foreach (var run in block.FontRuns.Where(r => r.RunKind != FontRunKind.TrailingWhitespace && r.Glyphs.Length > 0))
        {
            // 正体フォントのグリフ自体を傾け、複数行の座標やアラビア語の組版位置を維持します。
            using var font = new SKFont(run.Typeface, run.Style.FontSize) { SkewX = -0.2f, Subpixel = true, Edging = SKFontEdging.Antialias };
            using var builder = new SKTextBlobBuilder();
            var buffer = builder.AllocatePositionedRun(font, run.Glyphs.Length);
            buffer.SetGlyphs(run.Glyphs.AsSpan());
            buffer.SetPositions(run.GlyphPositions.AsSpan());
            using var blob = builder.Build();
            using var paint = new SKPaint { Color = run.Style.TextColor, IsAntialias = true };
            canvas.DrawText(blob, 0, 0, paint);
            float baseline = run.Line.YCoord + run.Line.BaseLine;
            if (run.Style.Underline != UnderlineStyle.None)
            {
                paint.StrokeWidth = Math.Max(1, font.Metrics.UnderlineThickness ?? 1);
                float y = baseline + Math.Max(1, font.Metrics.UnderlinePosition ?? 1);
                canvas.DrawLine(run.XCoord, y, run.XCoord + run.Width, y, paint);
            }
            if (run.Style.StrikeThrough != StrikeThroughStyle.None)
            {
                paint.StrokeWidth = Math.Max(1, font.Metrics.StrikeoutThickness ?? 1);
                float y = baseline + (font.Metrics.StrikeoutPosition ?? -run.Style.FontSize * 0.3f);
                canvas.DrawLine(run.XCoord, y, run.XCoord + run.Width, y, paint);
            }
        }
    }
}
