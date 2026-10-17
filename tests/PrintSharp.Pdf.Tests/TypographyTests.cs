using PrintSharp.Documents;
using PrintSharp.Layout;
using PrintSharp.Styles;
using PdfReader = UglyToad.PdfPig.PdfDocument;
using Xunit;

namespace PrintSharp.Pdf.Tests;

public sealed class TypographyTests
{
    private static Document Create(string text, CellStyle style) => Document.Create(d => d.Page(p => p
        .Columns(160).Rows(160).Cell(0, 0, c => c.Text(text).Style(style))));

    [Fact]
    public void JustificationExpandsWordGapsButPreservesTheFinalAndExplicitLines()
    {
        const string text = "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda\nshort end";
        var style = new CellStyle { WrapText = true, Font = new("NotoSans", 16), VerticalAlignment = VerticalAlignment.Top };
        var document = Create(text, style);
        var content = GridLayoutEngine.Instance.CalculateDocument(document).Pages[0].Cells[0].ContentBounds;
        using var left = PdfReader.Open(document.ToPdfBytes());
        using var justified = PdfReader.Open(Create(text, style with { HorizontalAlignment = HorizontalAlignment.Justify }).ToPdfBytes());
        var leftLines = LineEnds(left);
        var justifiedLines = LineEnds(justified);
        Assert.True(justifiedLines.Length > 2);
        Assert.Equal(leftLines.Length, justifiedLines.Length);
        Assert.True(justifiedLines[0] > leftLines[0] + 1);
        Assert.InRange(Math.Abs(justifiedLines[0] - content.Right), 0, 1);
        Assert.Equal(leftLines[^1], justifiedLines[^1], 2);
        Assert.Equal(leftLines[^2], justifiedLines[^2], 2);
    }

    private static double[] LineEnds(PdfReader pdf) => pdf.GetPage(1).Letters
        .GroupBy(letter => Math.Round(letter.StartBaseLine.Y, 2)).OrderByDescending(group => group.Key)
        .Select(group => group.Max(letter => letter.EndBaseLine.X)).ToArray();

    [Fact]
    public void SimulatedItalicPreservesVectorTextAndDecorations()
    {
        var style = new CellStyle
        {
            WrapText = true,
            Font = new("NotoSans", 16, italic: true, underline: true, strikeThrough: true)
        };
        using var pdf = PdfReader.Open(Create("Italic ABC\nالعربية مرحبًا", style).ToPdfBytes());
        var page = pdf.GetPage(1);
        Assert.NotEmpty(page.Letters);
        Assert.Equal(0, page.NumberOfImages);
        Assert.Contains(page.Paths, path => path.IsStroked);
        Assert.Contains(page.Letters, letter => Math.Abs(letter.BoundingBox.TopLeft.X - letter.BoundingBox.BottomLeft.X) > 0.01);
    }
}
