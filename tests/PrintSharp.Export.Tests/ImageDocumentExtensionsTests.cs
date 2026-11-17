using PrintSharp.Documents;
using PrintSharp.Fluent;
using PrintSharp.Image;
using SkiaSharp;
using Xunit;

namespace PrintSharp.Export.Tests;

public class ImageDocumentExtensionsTests
{
    [Fact]
    public void ToPageImages_UsesPhysicalPageOrderAndEncodesIdenticalPixels()
    {
        var document = Document.Create(d => d
            .Page("First", p => p.Settings(s => s.Size(72, 36).Margins(2)).Columns(60).Rows(20).Cell("A1", "First"))
            .Page("Second", p => p.Settings(s => s.Size(36, 72).Margins(2)).Columns(30).Rows(20).Cell("A1", "Second")));

        var png = document.ToPageImages(PageImageFormat.Png, 300);
        var webp = document.ToPageImages(PageImageFormat.WebP, 300);

        Assert.Equal(2, png.Count);
        Assert.Equal(2, webp.Count);
        for (int i = 0; i < 2; i++)
        {
            using var pngData = SKData.CreateCopy(png[i]);
            using var webpData = SKData.CreateCopy(webp[i]);
            using var pngCodec = SKCodec.Create(pngData);
            using var webpCodec = SKCodec.Create(webpData);
            Assert.Equal(SKEncodedImageFormat.Png, pngCodec.EncodedFormat);
            Assert.Equal(SKEncodedImageFormat.Webp, webpCodec.EncodedFormat);
            Assert.Equal(i == 0 ? 300 : 150, webpCodec.Info.Width);
            Assert.Equal(i == 0 ? 150 : 300, webpCodec.Info.Height);
            using var pngBitmap = SKBitmap.Decode(pngCodec);
            using var webpBitmap = SKBitmap.Decode(webpCodec);
            Assert.Equal(pngBitmap.Pixels, webpBitmap.Pixels);
            Assert.Contains(webpBitmap.Pixels, pixel => pixel != SKColors.White);
        }
    }
}
