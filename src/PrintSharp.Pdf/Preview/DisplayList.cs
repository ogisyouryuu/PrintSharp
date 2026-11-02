using System.Text.Json.Serialization;
using SkiaSharp;

namespace PrintSharp.Pdf.Preview;

/// <summary>バージョン 1 のページ描画命令。座標は左上原点の PDF ポイントです。</summary>
public sealed record DisplayPage(int PageNumber, float Width, float Height, IReadOnlyList<DisplayCommand> Commands)
{
    /// <summary>プロトコルのメジャーバージョン。</summary>
    public int SchemaVersion => 1;
    /// <summary>座標単位。</summary>
    public string Unit => "pt";
}

/// <summary>有限の描画命令。未使用のフィールドは null です。</summary>
public sealed record DisplayCommand(string Op)
{
    /// <summary>矩形または線分の座標。</summary>
    public IReadOnlyList<float>? Values { get; init; }
    /// <summary>#RRGGBBAA 形式の色。</summary>
    public string? Color { get; init; }
    /// <summary>線幅。</summary>
    public float? StrokeWidth { get; init; }
    /// <summary>破線の長さと間隔。</summary>
    public IReadOnlyList<float>? Dash { get; init; }
    /// <summary>画像リソースの識別子。</summary>
    public string? ResourceId { get; init; }
    /// <summary>最終位置が確定した字形列。</summary>
    public IReadOnlyList<DisplayGlyph>? Glyphs { get; init; }
    [JsonIgnore] internal PdfGlyphRun? PdfRun { get; init; }
}

/// <summary>1 ポイントの輪郭を Size 倍して基線位置へ移動します。</summary>
public sealed record DisplayGlyph(string ResourceId, float X, float Y, float Size);

/// <summary>文書内で共有する輪郭または PNG 画像。</summary>
public sealed record DisplayResource(string Kind, string Data);

internal sealed record PdfGlyphRun(SKTypeface Typeface, float Size, float Skew, ushort[] Glyphs, SKPoint[] Positions);

/// <summary>確定済みページとリソースを保持する文書。元の文書の変更を反映しません。</summary>
public sealed class PreviewDocument
{
    /// <summary>物理ページの読み取り専用一覧。</summary>
    public IReadOnlyList<DisplayPage> Pages { get; }
    /// <summary>文書内の共有リソース。</summary>
    public IReadOnlyDictionary<string, DisplayResource> Resources { get; }
    private readonly SKDocumentPdfMetadata _metadata;

    internal PreviewDocument(IReadOnlyList<DisplayPage> pages, IReadOnlyDictionary<string, DisplayResource> resources, SKDocumentPdfMetadata metadata)
    {
        Pages = pages.Select(p => p with { Commands = p.Commands.Select(c => c with {
            Values = c.Values?.ToList().AsReadOnly(), Dash = c.Dash?.ToList().AsReadOnly(), Glyphs = c.Glyphs?.ToList().AsReadOnly()
        }).ToList().AsReadOnly() }).ToList().AsReadOnly();
        Resources = new System.Collections.ObjectModel.ReadOnlyDictionary<string, DisplayResource>(resources.ToDictionary());
        _metadata = metadata;
    }

    /// <summary>同じ描画計画から検索可能な PDF 文字を出力します。</summary>
    public byte[] ToPdfBytes(CancellationToken cancellationToken = default)
    {
        using var output = new MemoryStream();
        using var pdf = SKDocument.CreatePdf(output, _metadata) ?? throw new InvalidOperationException("Unable to create PDF.");
        foreach (var page in Pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DrawPage(pdf.BeginPage(page.Width, page.Height), page);
            pdf.EndPage();
        }
        pdf.Close();
        return output.ToArray();
    }

    /// <summary>確定済みページを Skia キャンバスへ再生します。</summary>
    public void DrawPage(SKCanvas canvas, DisplayPage page)
    {
        foreach (var command in page.Commands)
        {
            var v = command.Values;
            using var paint = new SKPaint { IsAntialias = true, Color = command.Color is null ? SKColors.Black : new SKColor(Convert.ToByte(command.Color.Substring(1, 2), 16), Convert.ToByte(command.Color.Substring(3, 2), 16), Convert.ToByte(command.Color.Substring(5, 2), 16), Convert.ToByte(command.Color.Substring(7, 2), 16)) };
            paint.StrokeWidth = command.StrokeWidth ?? 1;
            using var dash = command.Dash is { Count: > 0 } d ? SKPathEffect.CreateDash(d.ToArray(), 0) : null;
            paint.PathEffect = dash;
            switch (command.Op)
            {
                case "save": canvas.Save(); break;
                case "restore": canvas.Restore(); break;
                case "clipRect": canvas.ClipRect(SKRect.Create(v![0], v[1], v[2], v[3])); break;
                case "fillRect": canvas.DrawRect(v![0], v[1], v[2], v[3], paint); break;
                case "line": canvas.DrawLine(v![0], v[1], v[2], v[3], paint); break;
                case "image":
                    using (var image = SKImage.FromEncodedData(Convert.FromBase64String(Resources[command.ResourceId!].Data)))
                    {
                        paint.FilterQuality = SKFilterQuality.High;
                        canvas.DrawImage(image, SKRect.Create(v![0], v[1], v[2], v[3]), paint);
                    }
                    break;
                case "glyphRun":
                    var run = command.PdfRun!;
                    using (var font = new SKFont(run.Typeface, run.Size) { SkewX = run.Skew, Subpixel = true, Edging = SKFontEdging.Antialias })
                    using (var builder = new SKTextBlobBuilder())
                    {
                        var buffer = builder.AllocatePositionedRun(font, run.Glyphs.Length);
                        buffer.SetGlyphs(run.Glyphs);
                        buffer.SetPositions(run.Positions);
                        using var blob = builder.Build();
                        canvas.DrawText(blob, 0, 0, paint);
                    }
                    break;
                default: throw new InvalidDataException($"Unknown display operation: {command.Op}");
            }
        }
    }
}
