using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Layout;
using PrintSharp.Styles;
using SkiaSharp;

namespace PrintSharp.Pdf;

/// <summary>共有グリッドレイアウトを Skia の PDF バックエンドに直接描画します。</summary>
public sealed class SkiaPdfRenderer
{
    /// <summary>既定のレンダラー。</summary>
    public static SkiaPdfRenderer Instance { get; } = new();
    private static readonly Lazy<IPdfTextRenderer> DefaultText = new(() => new NotoPdfTextRenderer(NotoFontRegistry.Default));

    /// <summary>PDF と読み取り専用のページ情報を生成します。</summary>
    public RenderedPdfDocument RenderToDocument(Document document, PdfRenderOptions? options = null)
    {
        using var stream = new MemoryStream();
        var pages = RenderCore(document, stream, options);
        var metadata = document.Metadata;
        return new(stream.ToArray(), pages, new(metadata.Title, metadata.Author, metadata.Subject, metadata.Keywords));
    }

    /// <summary>PDF を書き込みます。出力ストリームは閉じません。</summary>
    public void Render(Document document, Stream outputStream, PdfRenderOptions? options = null) => RenderCore(document, outputStream, options);

    /// <summary>PDF を指定したファイルに保存します。</summary>
    public void Render(Document document, string filePath, PdfRenderOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        using var result = RenderToDocument(document, options);
        result.Save(filePath);
    }

    /// <summary>PDF をバイト配列として生成します。</summary>
    public byte[] RenderToBytes(Document document, PdfRenderOptions? options = null)
    {
        using var stream = new MemoryStream();
        RenderCore(document, stream, options);
        return stream.ToArray();
    }

    private static IReadOnlyList<RenderedPdfPage> RenderCore(Document document, Stream output, PdfRenderOptions? options)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);
        if (!output.CanWrite) throw new ArgumentException("Output stream must be writable.", nameof(output));
        options ??= PdfRenderOptions.Default;
        float scale = options.PointsPerLogicalUnit;
        if (!float.IsFinite(scale) || scale <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "PointsPerLogicalUnit must be a finite positive number.");
        var layout = GridLayoutEngine.Instance.CalculateDocument(document);
        var metadata = document.Metadata;
        var pdfMetadata = SKDocumentPdfMetadata.Default;
        pdfMetadata.Title = metadata.Title;
        pdfMetadata.Author = metadata.Author;
        pdfMetadata.Subject = metadata.Subject;
        pdfMetadata.Keywords = metadata.Keywords;
        pdfMetadata.Creator = "PrintSharp";
        // Skia の整数デバイス座標による用紙寸法の丸めを 0.01 ポイントに抑えます。
        // このレンダラーでは高解像度の中間ビットマップを生む ImageFilter を使用しません。
        pdfMetadata.RasterDpi = 7200;
        var pages = new List<RenderedPdfPage>();
        using var pdf = SKDocument.CreatePdf(output, pdfMetadata) ?? throw new InvalidOperationException("Unable to create a PDF document.");
        foreach (var page in layout.Pages)
        {
            bool landscape = page.Page.Settings.Orientation == PageOrientation.Landscape;
            float width = page.TotalWidth > 0 ? page.TotalWidth * scale : landscape ? 842 : 595;
            float height = page.TotalHeight > 0 ? page.TotalHeight * scale : landscape ? 595 : 842;
            if (!float.IsFinite(width) || !float.IsFinite(height) || width > int.MaxValue / 100f || height > int.MaxValue / 100f)
                throw new ArgumentOutOfRangeException(nameof(options), "Scaled page dimensions exceed the PDF backend coordinate range.");
            width = MathF.Round(width * 100) / 100;
            height = MathF.Round(height * 100) / 100;
            var canvas = pdf.BeginPage(width, height);
            // 既存出力と同様、文字は背景・罫線・画像の上に配置します。
            foreach (var cell in page.Cells) DrawCellBackground(canvas, cell, scale);
            foreach (var cell in page.Cells) DrawCellText(canvas, cell, scale, options);
            pdf.EndPage();
            pages.Add(new(width, height));
        }
        pdf.Close();
        return pages.AsReadOnly();
    }

    private static void DrawCellBackground(SKCanvas canvas, CalculatedCellLayout layout, float scale)
    {
        var bounds = Scale(layout.Bounds, scale);
        if (layout.EffectiveStyle.BackColor is { A: > 0 } background)
        {
            using var paint = new SKPaint { Color = ToColor(background) };
            canvas.DrawRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, paint);
        }
        DrawBorders(canvas, bounds, layout.EffectiveStyle.Border, scale);
        if (layout.Cell.Type != CellType.Image || layout.Cell.Value is not byte[] { Length: > 0 } bytes) return;
        var content = Scale(layout.ContentBounds, scale);
        if (content.Width <= 0 || content.Height <= 0) return;
        using var image = SKImage.FromEncodedData(bytes) ?? throw new InvalidDataException("Unsupported cell image.");
        using var imagePaint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
        canvas.DrawImage(image, new SKRect(content.X, content.Y, content.Right, content.Bottom), imagePaint);
    }

    private static void DrawCellText(SKCanvas canvas, CalculatedCellLayout layout, float scale, PdfRenderOptions options)
    {
        var cell = layout.Cell;
        if (cell.Type is CellType.Blank or CellType.Image || cell.Value is null) return;
        string text = Convert.ToString(cell.Value, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
        if (cell.Type == CellType.Formula && text.StartsWith('=')) text = text[1..];
        var content = Scale(layout.ContentBounds, scale);
        if (text.Length == 0 || content.Width <= 0 || content.Height <= 0) return;
        var style = layout.EffectiveStyle;
        if (options.FontResolver is { } resolver)
        {
            var font = style.Font ?? FontSpec.Default;
            style = style with { Font = font with { Family = resolver.Resolve(font).ResolvedFamilyName } };
        }
        canvas.Save();
        try { (options.TextRenderer ?? DefaultText.Value).DrawText(canvas, text, style, content, scale); }
        finally { canvas.Restore(); }
    }

    private static LayoutRect Scale(LayoutRect rect, float scale) => new(rect.X * scale, rect.Y * scale, rect.Width * scale, rect.Height * scale);
    private static SKColor ToColor(ColorSpec color) => new(color.R, color.G, color.B, color.A);

    private static void DrawBorders(SKCanvas canvas, LayoutRect rect, BorderSpec? border, float scale)
    {
        if (border is null) return;
        DrawBorder(canvas, rect.X, rect.Y, rect.Right, rect.Y, border.Top, scale);
        DrawBorder(canvas, rect.X, rect.Bottom, rect.Right, rect.Bottom, border.Bottom, scale);
        DrawBorder(canvas, rect.X, rect.Y, rect.X, rect.Bottom, border.Left, scale);
        DrawBorder(canvas, rect.Right, rect.Y, rect.Right, rect.Bottom, border.Right, scale);
        DrawBorder(canvas, rect.X, rect.Y, rect.Right, rect.Bottom, border.Diagonal, scale);
    }

    private static void DrawBorder(SKCanvas canvas, float x1, float y1, float x2, float y2, BorderLine? line, float scale)
    {
        if (line is null || line.Style == BorderStyle.None) return;
        float width = line.Width > 0 ? line.Width * scale : line.Style switch
        {
            BorderStyle.Medium => 1.5f, BorderStyle.Thick => 2.5f, BorderStyle.Hair => 0.25f, _ => 0.75f
        };
        using var paint = new SKPaint
        {
            Color = line.Color is { } color ? ToColor(color) : SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = width,
            IsAntialias = true
        };
        using var dash = line.Style switch
        {
            BorderStyle.Dashed => SKPathEffect.CreateDash([3 * width, width], 0),
            BorderStyle.Dotted => SKPathEffect.CreateDash([width, 2 * width], 0),
            _ => null
        };
        paint.PathEffect = dash;
        canvas.DrawLine(x1, y1, x2, y2, paint);
        if (line.Style != BorderStyle.Double) return;
        // 互換性のため、二本目の罫線は従来と同じ 1.5 ポイントずらします。
        const float inset = 1.5f;
        if (Math.Abs(y2 - y1) < 0.0001f) canvas.DrawLine(x1, y1 + inset, x2, y2 + inset, paint);
        else canvas.DrawLine(x1 + inset, y1, x2 + inset, y2, paint);
    }
}
