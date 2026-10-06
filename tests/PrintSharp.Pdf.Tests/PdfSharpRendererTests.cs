using PrintSharp.Documents;
using Xunit;

namespace PrintSharp.Pdf.Tests;

public sealed class PdfSharpRendererTests
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
        Assert.Equal(148 * 72d / 25.4, pdf.Pages[0].Width.Point, 2);
        Assert.Equal(210 * 72d / 25.4, pdf.Pages[0].Height.Point, 2);
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
        document.AddPage("Second");

        using var pdf = document.ToPdfDocument();

        Assert.Equal(2, pdf.PageCount);
        Assert.Equal("Invoice", pdf.Info.Title);
        Assert.Equal("PrintSharp", pdf.Info.Author);
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
}
