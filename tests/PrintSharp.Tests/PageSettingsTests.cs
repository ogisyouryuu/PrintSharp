using PrintSharp.Documents;
using PrintSharp.Fluent;
using PrintSharp.Layout;
using Xunit;

namespace PrintSharp.Tests;

public class PageSettingsTests
{
    [Theory]
    [InlineData(PaperKind.A5, 148, 210)]
    [InlineData(PaperKind.B5, 176, 250)]
    [InlineData(PaperKind.A4, 210, 297)]
    [InlineData(PaperKind.B4, 250, 353)]
    [InlineData(PaperKind.A3, 297, 420)]
    public void StandardPaper_UsesPhysicalSizeAndOrientation(PaperKind kind, int width, int height)
    {
        var page = new Page { Settings = new PageSettingsBuilder().PaperKind(kind).Build() };
        var portrait = GridLayoutEngine.Instance.CalculatePage(page);
        Assert.Equal(width * 72f / 25.4f, portrait.TotalWidth, 3);
        Assert.Equal(height * 72f / 25.4f, portrait.TotalHeight, 3);
        page.Settings = page.Settings with { Orientation = PageOrientation.Landscape };
        var landscape = GridLayoutEngine.Instance.CalculatePage(page);
        Assert.Equal(portrait.TotalWidth, landscape.TotalHeight);
        Assert.Equal(portrait.TotalHeight, landscape.TotalWidth);
    }

    [Fact]
    public void Size_SelectsCustomerAndPreservesDimensions()
    {
        var settings = new PageSettingsBuilder().PaperKind(PaperKind.A4).Size(300, 400).Build();
        Assert.Equal(PaperKind.Customer, settings.PaperKind);
        Assert.Equal((300f, 400f), settings.GetPaperSize());
    }

    [Fact]
    public void PageProperties_StaySynchronizedWithMutableModel()
    {
        var document = new Document();
        var second = document.AddPage();
        Assert.Equal(2, document.PageCount);
        Assert.Equal(2, second.CurrentPageNumber);
        second.CurrentPageNumber = 7;
        Assert.Equal(7, second.PageNumber);
        document.Pages.Remove(second);
        Assert.Equal(1, document.PageCount);
        document.Pages.Clear();
        Assert.Equal(0, document.PageCount);
    }
}
