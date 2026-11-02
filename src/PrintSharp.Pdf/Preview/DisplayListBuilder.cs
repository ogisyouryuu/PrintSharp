using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace PrintSharp.Pdf.Preview;

/// <summary>PDF と Canvas の共通描画計画を構築します。</summary>
public sealed class DisplayListBuilder
{
    internal List<DisplayCommand> Commands { get; } = [];
    internal Dictionary<string, DisplayResource> Resources { get; }
    private readonly Dictionary<(SKTypeface, ushort, float), string> _glyphCache;

    internal DisplayListBuilder(Dictionary<string, DisplayResource> resources, Dictionary<(SKTypeface, ushort, float), string> glyphCache)
        => (Resources, _glyphCache) = (resources, glyphCache);

    /// <summary>描画順を維持して命令を追加します。</summary>
    public void Add(DisplayCommand command) => Commands.Add(command);
    internal static string Color(SKColor c) => $"#{c.Red:x2}{c.Green:x2}{c.Blue:x2}{c.Alpha:x2}";
    internal string Resource(string kind, string data)
    {
        var id = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(kind + data)));
        Resources.TryAdd(id, new(kind, data));
        return id;
    }
    internal void DrawRect(float x, float y, float width, float height, SKPaint paint)
        => Add(new("fillRect") { Values = [x, y, width, height], Color = Color(paint.Color) });
    internal void DrawLine(float x1, float y1, float x2, float y2, SKPaint paint, float[]? dash = null)
        => Add(new("line") { Values = [x1, y1, x2, y2], Color = Color(paint.Color), StrokeWidth = paint.StrokeWidth, Dash = dash });
    internal void DrawImage(SKImage image, SKRect rect, SKPaint paint)
    {
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        Add(new("image") { Values = [rect.Left, rect.Top, rect.Width, rect.Height], ResourceId = Resource("png", Convert.ToBase64String(data.ToArray())) });
    }
    /// <summary>組版済み字形列と共有輪郭を追加します。Typeface は PDF 出力完了まで保持してください。</summary>
    public void DrawGlyphRun(SKTypeface face, float size, float skew, ushort[] glyphs, SKPoint[] positions, SKColor color)
    {
        using var font = new SKFont(face, 1) { SkewX = skew };
        var instances = new List<DisplayGlyph>();
        for (int i = 0; i < glyphs.Length; i++)
        {
            var key = (face, glyphs[i], skew);
            if (!_glyphCache.TryGetValue(key, out var id))
            {
                using var path = font.GetGlyphPath(glyphs[i]);
                id = Resource("path", path?.ToSvgPathData() ?? "");
                _glyphCache.Add(key, id);
            }
            instances.Add(new(id, positions[i].X, positions[i].Y, size));
        }
        Add(new("glyphRun") { Color = Color(color), Glyphs = instances.AsReadOnly(), PdfRun = new(face, size, skew, glyphs, positions) });
    }
}
