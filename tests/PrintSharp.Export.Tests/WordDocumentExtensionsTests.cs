using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using DocumentFormat.OpenXml.Wordprocessing;
using PrintSharp.Cells;
using PrintSharp.Fluent;
using PrintSharp.Styles;
using PrintSharp.Word;
using Xunit;
using GridDocument = PrintSharp.Documents.Document;

namespace PrintSharp.Export.Tests;

public class WordDocumentExtensionsTests
{
    [Fact]
    public void SaveAsDocx_WritesEditableMergedCellsAndResolvesPageValues()
    {
        var document = GridDocument.Create(d => d
            .Page("First", p => p.Settings(s => s.Size(150, 100).Margins(10)).Columns(40, 40).Rows(20, 30))
            .Page("Second", p => p.Settings(s => s.Size(120, 90).Margins(10)).Columns(70).Rows(20)));
        document.DefaultPage.SetCell(new Cell(0, 0, "見出し\nSecond line", rowSpan: 2, columnSpan: 2,
            style: new CellStyle { WrapText = true, BackColor = ColorSpec.Blue, Border = BorderSpec.All(BorderStyle.Thin) }));
        document.Pages[1].SetCell(new Cell(0, 0, new PrintSharp.Documents.PageValue(ctx => $"{ctx.CurrentPageNumber}/{ctx.PageCount}")));

        using var stream = new MemoryStream();
        document.SaveAsDocx(stream);
        Assert.True(stream.CanWrite);
        stream.Position = 0;
        using var word = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(word));
        var body = word.MainDocumentPart!.Document.Body!;
        var tables = body.Elements<Table>().ToArray();
        Assert.Equal(2, tables.Length);
        Assert.Equal(new[] { "見出し", "Second line" }, tables[0].Descendants<Text>().Select(t => t.Text));
        Assert.Single(tables[0].Descendants<Break>());
        Assert.All(tables[0].Descendants<GridSpan>(), span => Assert.Equal(2, span.Val!.Value));
        Assert.Equal(new[] { MergedCellValues.Restart, MergedCellValues.Continue },
            tables[0].Descendants<VerticalMerge>().Select(merge => merge.Val!.Value));
        Assert.Equal(new uint[] { 360, 560 }, tables[0].Descendants<TableRowHeight>().Select(height => height.Val!.Value));
        Assert.Equal("2/2", tables[1].InnerText);
        Assert.Equal(new uint[] { 3000, 2400 }, body.Descendants<PageSize>().Select(size => size.Width!.Value));
    }

    [Fact]
    public void ToDocxBytes_EmbedsCellImage()
    {
        byte[] image = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jc1sAAAAASUVORK5CYII=");
        var document = new GridDocument();
        document.DefaultPage.SetCell(new Cell(0, 0, image, type: CellType.Image));

        using var stream = new MemoryStream(document.ToDocxBytes());
        using var word = WordprocessingDocument.Open(stream, false);
        Assert.Empty(new OpenXmlValidator().Validate(word));
        var part = word.MainDocumentPart!;
        Assert.Equal("image/png", Assert.Single(part.ImageParts).ContentType);
        Assert.Single(part.Document.Descendants<Drawing>());
    }
}
