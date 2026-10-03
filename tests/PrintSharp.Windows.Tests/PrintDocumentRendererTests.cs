using Xunit;
using PrintSharp.Documents;
using PrintSharp.Windows;

namespace PrintSharp.Windows.Tests;

public sealed class PrintDocumentRendererTests
{
    [Fact]
    public void ToPrintDocument_UsesDocumentTitleAndDefaultOrientation()
    {
        var document = new Document();
        document.Metadata.Title = "Sales report";
        document.DefaultPage.Settings = document.DefaultPage.Settings with
        {
            Orientation = PageOrientation.Landscape
        };

        using var printDocument = document.ToPrintDocument();

        Assert.Equal("Sales report", printDocument.DocumentName);
        Assert.True(printDocument.DefaultPageSettings.Landscape);
    }

    [Fact]
    public void ToPrintDocument_ProvidesFallbackNameWhenTitleIsMissing()
    {
        var document = new Document();

        using var printDocument = document.ToPrintDocument();

        Assert.Equal("PrintSharp Document", printDocument.DocumentName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void ToPrintDocument_RejectsInvalidDimensionScale(float scale)
    {
        var document = new Document();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            document.ToPrintDocument(new PrintDocumentRenderOptions { PointsPerLogicalUnit = scale }));
    }
}
