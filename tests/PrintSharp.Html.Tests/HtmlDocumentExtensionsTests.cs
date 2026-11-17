using System.Globalization;
using System.Text;
using PrintSharp.Cells;
using PrintSharp.Documents;
using PrintSharp.Fluent;
using PrintSharp.Grid;
using PrintSharp.Styles;
using Xunit;

namespace PrintSharp.Html.Tests;

public class HtmlDocumentExtensionsTests
{
    [Fact]
    public void ToHtml_UsesGridGeometryAndEscapesContentAndFontNames()
    {
        var document = Document.Create(d => d.Title("<Report>").Page("Sheet", p => p
            .Settings(s => s.Size(120, 100).Margins(10))
            .Columns(30, 40)
            .Rows(20, 25, 15)));
        document.DefaultPage.Rows[2].IsHidden = true;
        document.DefaultPage.SetCell(new Cell(0, 0, "<script>&", rowSpan: 2, columnSpan: 2, style: new CellStyle
        {
            Font = new FontSpec("x';color:red", bold: true),
            BackColor = ColorSpec.Blue,
            Border = BorderSpec.All(BorderStyle.Dashed)
        }));
        document.DefaultPage.SetCell(new Cell(2, 0, "Hidden row"));

        string html = document.ToHtml();

        Assert.Contains("<title>&lt;Report&gt;</title>", html);
        Assert.Contains("&lt;script&gt;&amp;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("x';color:red", html);
        Assert.Contains("left:10pt;top:10pt;width:70pt;height:45pt;", html);
        Assert.Contains("class=\"content\" style=\"left:13pt;top:12pt;width:64pt;height:41pt;", html);
        Assert.Contains("background:#0000FFFF;", html);
        Assert.Contains("border-top:0.75pt dashed #000000FF;", html);
        Assert.DoesNotContain("Hidden row", html);
    }

    [Fact]
    public void ToHtml_RespectsInheritedAndExplicitZeroPadding()
    {
        var document = Document.Create(d => d.Page("Sheet", p => p
            .Settings(s => s.Size(120, 100).Margins(10))
            .Columns(30, 40)
            .Rows(20)));
        var page = document.DefaultPage;
        page.Rows[0].DefaultStyle = new CellStyle { Padding = new PaddingSpec(5, 4, 3, 2) };
        page.SetCell(new Cell(0, 0, "Inherited padding"));
        page.SetCell(new Cell(0, 1, "Zero padding", style: new CellStyle { Padding = PaddingSpec.Zero }));

        string html = document.ToHtml();

        Assert.Contains("class=\"content\" style=\"left:15pt;top:14pt;width:22pt;height:14pt;", html);
        Assert.Contains("class=\"content\" style=\"left:40pt;top:10pt;width:40pt;height:20pt;", html);
    }

    [Fact]
    public void ToHtml_PaginatesAndFormatsDeferredValuesUsingDocumentCulture()
    {
        var document = Document.Create(d => d.Page("Report", p => p
            .Settings(s => s.Size(100, 60).Margins(10))
            .Columns(50)
            .Report(r => r
                .Body(new[] { 1.5m, 2.5m }, 25, (row, value) => row.Cell(value))
                .Footer(f => f.Row(10, row => row.Cell(new PageValue(ctx =>
                    $"{ctx.CurrentPageNumber}/{ctx.PageCount}")))))));
        document.Culture = CultureInfo.GetCultureInfo("de-DE");

        using var stream = new MemoryStream();
        document.SaveAsHtml(stream);
        string html = Encoding.UTF8.GetString(stream.ToArray());

        Assert.True(stream.CanWrite);
        Assert.Contains("lang=\"de-DE\"", html);
        Assert.Contains("@page sheet0{size:100pt 60pt;margin:0}", html);
        Assert.Contains("@page sheet1{size:100pt 60pt;margin:0}", html);
        Assert.DoesNotContain("@page sheet2", html);
        Assert.Contains(">1,5</span>", html);
        Assert.Contains(">2,5</span>", html);
        Assert.Contains(">1/2</span>", html);
        Assert.Contains(">2/2</span>", html);
        Assert.Single(document.Pages);
    }
}
