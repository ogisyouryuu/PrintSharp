using PrintSharp.Documents;
using PrintSharp.Cells;
using PrintSharp.Layout;
using PrintSharp.Styles;
using SkiaSharp;
using PdfReader = UglyToad.PdfPig.PdfDocument;
using Xunit;

namespace PrintSharp.Pdf.Tests;

public sealed class SkiaPdfRendererTests
{
    [Fact]
    public void AutomaticPagination_ExportsPhysicalPagesWithoutMutatingLogicalDocument()
    {
        var document = Document.Create(d => d.Page(p => p
            .Settings(s => s.PaperKind(PaperKind.A5).Margins(10))
            .Rows(Enumerable.Repeat(100f, 10).ToArray())
            .Paginate()));
        using var pdf = document.ToPdfDocument();
        Assert.Equal(2, pdf.PageCount);
        Assert.Equal(1, document.PageCount);
        Assert.Equal(148 * 72d / 25.4, (double)pdf.Pages[0].Width, 2);
        Assert.Equal(210 * 72d / 25.4, (double)pdf.Pages[0].Height, 2);
        using var parsed = PdfReader.Open(pdf.ToArray());
        Assert.Equal(2, parsed.NumberOfPages);
        Assert.Equal(148 * 72d / 25.4, parsed.GetPage(1).Width, 2);
        Assert.Equal(210 * 72d / 25.4, parsed.GetPage(1).Height, 2);
    }
    [Fact]
    public void ToPdfBytes_ReturnsPdfFile()
    {
        var document = new Document();

        byte[] bytes = document.ToPdfBytes();

        Assert.NotEmpty(bytes);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Fact]
    public void ToPdfDocument_PreservesMetadataAndRendersEveryPage()
    {
        var document = new Document();
        document.Metadata.Title = "Invoice";
        document.Metadata.Author = "PrintSharp";
        document.Metadata.Subject = "多言語";
        document.Metadata.Keywords = "memo,report";
        document.AddPage("Second");

        using var pdf = document.ToPdfDocument();

        Assert.Equal(2, pdf.PageCount);
        Assert.Equal("Invoice", pdf.Info.Title);
        Assert.Equal("PrintSharp", pdf.Info.Author);
        using var parsed = PdfReader.Open(pdf.ToArray());
        Assert.Equal(2, parsed.NumberOfPages);
        Assert.Equal("Invoice", parsed.Information.Title);
        Assert.Equal("PrintSharp", parsed.Information.Author);
        Assert.Equal("多言語", parsed.Information.Subject);
        Assert.Equal("memo,report", parsed.Information.Keywords);
    }

    [Fact]
    public void SaveAsPdf_StreamWritesPdfAndLeavesStreamOpen()
    {
        var document = new Document();
        using var stream = new MemoryStream();

        document.SaveAsPdf(stream);

        Assert.True(stream.CanWrite);
        Assert.True(stream.Length > 0);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(stream.ToArray(), 0, 5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void RenderToDocument_RejectsInvalidDimensionScale(float scale)
    {
        var document = new Document();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            document.ToPdfDocument(new PdfRenderOptions { PointsPerLogicalUnit = scale }));
    }

    [Fact]
    public void DefaultRendererEmbedsMultilingualVectorTextWithoutApiBootstrap()
    {
        var document = Document.Create(d => d.Page(p => p.Columns(400).Rows(100)
            .Cell(0, 0, c => c.Text("Memo: 中文 日本語 한국어 العربية مَرْحَبًا (ABC-123)")
                .Style(new CellStyle { WrapText = true }))));
        using var pdf = PdfReader.Open(document.ToPdfBytes());
        var page = pdf.GetPage(1);
        Assert.NotEmpty(page.Letters);
        Assert.Equal(0, page.NumberOfImages);
        Assert.DoesNotContain(page.Letters, letter => letter.Value == "\uFFFD");
    }

    [Fact]
    public void ImageCellIsEmbeddedAtScaledMergedContentBounds()
    {
        using var bitmap = new SKBitmap(4, 4);
        bitmap.Erase(SKColors.Red);
        using var encoded = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        var document = Document.Create(d => d.Page(p => p.Columns(60, 80).Rows(50)
            .Cell(0, 0, 1, 2, c => c.Image(encoded.ToArray()))));
        var content = GridLayoutEngine.Instance.CalculateDocument(document).Pages[0].Cells[0].ContentBounds;
        using var pdf = PdfReader.Open(document.ToPdfBytes(new PdfRenderOptions { PointsPerLogicalUnit = 2 }));
        var image = Assert.Single(pdf.GetPage(1).GetImages());
        Assert.Equal(content.Width * 2, image.BoundingBox.Width, 2);
        Assert.Equal(content.Height * 2, image.BoundingBox.Height, 2);
    }

    [Theory]
    [InlineData(BorderStyle.Thin)]
    [InlineData(BorderStyle.Medium)]
    [InlineData(BorderStyle.Thick)]
    [InlineData(BorderStyle.Hair)]
    [InlineData(BorderStyle.Dashed)]
    [InlineData(BorderStyle.Dotted)]
    [InlineData(BorderStyle.Double)]
    public void BordersAndBackgroundRemainVectorPaths(BorderStyle borderStyle)
    {
        var line = new BorderLine(borderStyle, ColorSpec.Red);
        var style = new CellStyle { BackColor = ColorSpec.Blue, Border = new(line, line, line, line, line) };
        var document = Document.Create(d => d.Page(p => p.Columns(80).Rows(40)
            .Cell(0, 0, c => c.Type(CellType.Blank).Style(style))));
        using var pdf = PdfReader.Open(document.ToPdfBytes());
        var page = pdf.GetPage(1);
        Assert.Contains(page.Paths, path => path.IsFilled);
        Assert.True(page.Paths.Count >= 2);
        Assert.All(page.Paths, path => Assert.True(path.IsStroked || path.IsFilled));
        Assert.Equal(0, page.NumberOfImages);
        Assert.Empty(page.Letters);
    }

    [Fact]
    public void SnapshotCanBeSavedRepeatedlyAndDoesNotExposeMutableBytes()
    {
        var document = new Document();
        document.Metadata.Title = "Original";
        using var snapshot = document.ToPdfDocument();
        document.Metadata.Title = "Changed";
        byte[] modifiedCopy = snapshot.ToArray();
        modifiedCopy[0] = 0;
        using var output = new MemoryStream();
        snapshot.Save(output);
        Assert.True(output.CanWrite);
        using var parsed = PdfReader.Open(output.ToArray());
        Assert.Equal("Original", parsed.Information.Title);
        snapshot.Save(output, closeStream: true);
        Assert.False(output.CanWrite);
        snapshot.Dispose();
        Assert.Throws<ObjectDisposedException>(() => snapshot.ToArray());
    }

    [Fact]
    public void CustomTextRendererReceivesScaledContentAndCanDrawToPdf()
    {
        var renderer = new RecordingTextRenderer();
        var document = Document.Create(d => d.Page(p => p.Columns(80).Rows(40).Cell(0, 0, "Custom")));
        var content = GridLayoutEngine.Instance.CalculateDocument(document).Pages[0].Cells[0].ContentBounds;
        using var pdf = PdfReader.Open(document.ToPdfBytes(new PdfRenderOptions { TextRenderer = renderer, PointsPerLogicalUnit = 2 }));
        Assert.Equal(content.Width * 2, renderer.Bounds.Width);
        Assert.Equal(2, renderer.Scale);
        Assert.Equal("Custom", renderer.Text);
        Assert.Contains(pdf.GetPage(1).Paths, path => path.IsFilled);
    }

    private sealed class RecordingTextRenderer : IPdfTextRenderer
    {
        public LayoutRect Bounds { get; private set; }
        public float Scale { get; private set; }
        public string? Text { get; private set; }
        public void DrawText(SKCanvas canvas, string text, CellStyle style, LayoutRect content, float scale)
        {
            Bounds = content;
            Scale = scale;
            Text = text;
            using var paint = new SKPaint { Color = SKColors.Green };
            canvas.DrawRect(content.X, content.Y, content.Width, content.Height, paint);
        }
    }
}
