using System.Text.Json;
using PrintSharp.Documents;
using PrintSharp.Pdf.Preview;
using PrintSharp.Styles;
using SkiaSharp;
using Xunit;

namespace PrintSharp.Pdf.Tests;

public sealed class PreviewTests
{
    [Theory]
    [InlineData(null, 1, 3, 2)]
    [InlineData(7f, 2, 14, 14)]
    [InlineData(0f, 1, 0, 0)]
    public void TextPaddingIsScaledAndExplicitCellPaddingOverridesTheDefault(float? padding, float scale, float x, float y)
    {
        var style = new CellStyle { Font = new("NotoSans", 12), HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top, Padding = padding is { } p ? new PaddingSpec(p) : null };
        var document = Document.Create(d => d.Page(p => p.Settings(s => s.Margins(0)).Columns(100).Rows(40)
            .Cell(0, 0, c => c.Text("Alpha").Style(style))));
        var snapshot = SkiaPdfRenderer.Instance.CreatePreview(document, new() { PointsPerLogicalUnit = scale });
        var commands = snapshot.Pages[0].Commands;
        var clip = Assert.Single(commands, c => c.Op == "clipRect").Values!;
        Assert.Equal(x, clip[0]); Assert.Equal(y, clip[1]);
        Assert.Equal(100 * scale - 2 * x, clip[2]); Assert.Equal(40 * scale - 2 * y, clip[3]);
        var glyph = Assert.Single(commands, c => c.Op == "glyphRun").Glyphs![0];
        Assert.Equal(x, glyph.X, 3);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(snapshot.ToPdfBytes());
        Assert.Equal((double)glyph.X, pdf.GetPage(1).Letters[0].StartBaseLine.X, 2);
    }

    [Fact]
    public void ConfigurableDefaultPaddingClampsEmptyContentWithoutMovingBorders()
    {
        var document = Document.Create(d => d.Page(p => p.Settings(s => s.Margins(0)).Columns(100).Rows(40)
            .Cell(0, 0, c => c.Text("Alpha").Style(new CellStyle { BackColor = ColorSpec.Blue }))));
        var custom = SkiaPdfRenderer.Instance.CreatePreview(document, new() { DefaultTextPadding = new(8, 4, 6, 2) });
        Assert.Equal(new float[] { 8, 4, 86, 34 }, custom.Pages[0].Commands.Single(c => c.Op == "clipRect").Values);
        Assert.Equal(new float[] { 0, 0, 100, 40 }, custom.Pages[0].Commands.Single(c => c.Op == "fillRect").Values);
        var empty = SkiaPdfRenderer.Instance.CreatePreview(document, new() { DefaultTextPadding = new(200) });
        Assert.DoesNotContain(empty.Pages[0].Commands, c => c.Op == "glyphRun");
        Assert.Contains(empty.Pages[0].Commands, c => c.Op == "fillRect");
        Assert.Throws<ArgumentOutOfRangeException>(() => SkiaPdfRenderer.Instance.CreatePreview(document,
            new() { DefaultTextPadding = new PaddingSpec { Left = float.NaN } }));
    }

    [Fact]
    public void SharedPlanPreservesPaginationAndDeduplicatesGlyphs()
    {
        var document = Document.Create(d => d.Page(p => p.Settings(s => s.Size(200, 200).Margins(10))
            .Columns(100).Rows(100, 100, 100).Cell(0, 0, "AAA").Cell(1, 0, "AAA").Cell(2, 0, "AAA").Paginate()));
        var preview = SkiaPdfRenderer.Instance.CreatePreview(document);
        Assert.Equal(3, preview.Pages.Count);
        Assert.Single(preview.Resources);
        Assert.Equal(3, preview.Pages[0].Commands.Single(c => c.Op == "glyphRun").Glyphs!.Count);
        document.Metadata.Title = "Changed";
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(preview.ToPdfBytes());
        Assert.Equal(preview.Pages.Count, pdf.NumberOfPages);
        Assert.Equal(200, pdf.GetPage(1).Width);
        Assert.NotEqual("Changed", pdf.Information.Title);
        Assert.NotEmpty(pdf.GetPage(1).Letters);
    }

    [Fact]
    public void BrowserFixtureUsesMultilingualPathsImagesClippingAndDecorations()
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Red);
        using var image = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var line = new BorderLine(BorderStyle.Dashed, ColorSpec.Blue);
        var document = Document.Create(d => d.Title("Preview fixture").Page(p => p
            .Settings(s => s.Size(400, 300).Margins(12)).Columns(250, 120).Rows(100, 90, 60)
            .Cell(0, 0, c => c.Text("中文 日本語 한국어\nالعربية مَرْحَبًا (ABC-123)").Style(new CellStyle {
                WrapText = true, Font = new("NotoSansCJKjp", 16), BackColor = new ColorSpec(230, 240, 250), Border = new(line, line, line, line) }))
            .Cell(0, 1, c => c.Image(image.ToArray()))
            .Cell(1, 0, c => c.Text("alpha beta gamma delta epsilon zeta eta theta iota end").Style(new CellStyle {
                WrapText = true, HorizontalAlignment = HorizontalAlignment.Justify, Font = new("NotoSans", 16, italic: true, underline: true, strikeThrough: true) }))
            .Cell(2, 0, c => c.Text("Clipped very long text outside the cell bounds 123456789").Style(new CellStyle { Font = new("NotoSans", 22) }))));
        var preview = SkiaPdfRenderer.Instance.CreatePreview(document);
        var page = Assert.Single(preview.Pages);
        Assert.Contains(preview.Resources.Values, r => r.Kind == "path" && r.Data.Length > 0);
        Assert.Contains(preview.Resources.Values, r => r.Kind == "png");
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PrintSharp.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var directory = Path.Combine(root.FullName, "artifacts", "preview-tests");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "page.json"), JsonSerializer.Serialize(new { documentId = "fixture", revision = "1", page, resources = preview.Resources }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        using var reference = new SKBitmap(400, 300);
        using (var canvas = new SKCanvas(reference)) { canvas.Clear(SKColors.White); preview.DrawPage(canvas, page); }
        using var png = reference.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(directory, "reference.png"), png.ToArray());
        File.WriteAllBytes(Path.Combine(directory, "document.pdf"), preview.ToPdfBytes());
    }
}
