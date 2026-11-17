using System.Globalization;
using PrintSharp.Documents;
using PrintSharp.Pdf;
using SkiaSharp;

namespace PrintSharp.Image;

/// <summary>ページ画像の出力形式です。</summary>
public enum PageImageFormat
{
    /// <summary>PNG 形式です。</summary>
    Png,

    /// <summary>可逆圧縮の WebP 形式です。</summary>
    WebP
}

/// <summary>グリッド文書のページ画像出力拡張です。</summary>
public static class ImageDocumentExtensions
{
    /// <summary>物理ページ順に画像を生成します。解像度の既定値は 144 DPI です。</summary>
    public static IReadOnlyList<byte[]> ToPageImages(this Document document, PageImageFormat format = PageImageFormat.Png, int dpi = 144)
        => RenderPages(document, format, dpi).ToList().AsReadOnly();

    /// <summary>指定したファイル名に 1 基準のページ番号を加えて、各ページを保存します。</summary>
    /// <remarks>例えば report.webp は report-1.webp、report-2.webp のように出力されます。</remarks>
    public static IReadOnlyList<string> SavePageImages(this Document document, string filePath, int dpi = 144)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        string extension = Path.GetExtension(filePath);
        var format = extension.ToLowerInvariant() switch
        {
            ".png" => PageImageFormat.Png,
            ".webp" => PageImageFormat.WebP,
            _ => throw new ArgumentException("Use a .png or .webp file extension.", nameof(filePath))
        };
        string stem = filePath[..^extension.Length];
        var paths = new List<string>();
        foreach (var image in RenderPages(document, format, dpi))
        {
            string path = string.Create(CultureInfo.InvariantCulture, $"{stem}-{paths.Count + 1}{extension}");
            File.WriteAllBytes(path, image);
            paths.Add(path);
        }
        return paths.AsReadOnly();
    }

    private static IEnumerable<byte[]> RenderPages(Document document, PageImageFormat format, int dpi)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dpi);
        if (format is not (PageImageFormat.Png or PageImageFormat.WebP))
            throw new ArgumentOutOfRangeException(nameof(format));

        double scale = dpi / 72d;
        var preview = SkiaPdfRenderer.Instance.CreatePreview(document);
        foreach (var page in preview.Pages)
        {
            double width = Math.Ceiling(page.Width * scale);
            double height = Math.Ceiling(page.Height * scale);
            if (width <= 0 || height <= 0 || width * height > int.MaxValue / 4 ||
                (format == PageImageFormat.WebP && (width > 16383 || height > 16383)))
                throw new InvalidOperationException($"Page {page.PageNumber} exceeds the image size limit at {dpi} DPI.");

            byte[] bytes;
            using (var bitmap = new SKBitmap((int)width, (int)height, SKColorType.Rgba8888, SKAlphaType.Opaque))
            {
                using (var canvas = new SKCanvas(bitmap))
                {
                    canvas.Clear(SKColors.White);
                    canvas.Scale((float)scale);
                    preview.DrawPage(canvas, page);
                    canvas.Flush();
                }
                using var pixels = bitmap.PeekPixels();
                using var data = format == PageImageFormat.WebP
                    ? pixels.Encode(new SKWebpEncoderOptions(SKWebpEncoderCompression.Lossless, 75))
                    : pixels.Encode(SKEncodedImageFormat.Png, 100);
                bytes = data?.ToArray() ?? throw new InvalidOperationException($"Unable to encode page {page.PageNumber}.");
            }
            yield return bytes;
        }
    }
}
