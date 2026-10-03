using ClosedXML.Excel;
using PrintSharp.Documents;
using PrintSharp.Excel;
using PrintSharp.Fluent;
using PrintSharp.Styles;
using Xunit;

namespace PrintSharp.Excel.Tests;

public class ExcelTemplateParserTests
{
    private record ItemDto(string Name, int Qty, decimal Price);

    [Fact]
    public void Parse_RoundTrip_ShouldReconstructGridDocument()
    {
        // 1. Build an initial document
        var originalDoc = Document.Create(d =>
        {
            d.Title("RoundTrip Test")
             .Page("Sheet1", p =>
             {
                 p.Columns(100f, 60f, 80f)
                  .Rows(30f, 22f)
                  .Cell("A1:C1", c => c.Value("Header Title").Bold().AlignCenter())
                  .Cell("A2", "Product A")
                  .Cell("B2", 10)
                  .Cell("C2", 99.5m);
             });
        });

        // 2. Export to bytes via ClosedXmlRenderer
        byte[] xlsxBytes = originalDoc.ToExcelBytes();

        // 3. Parse bytes back via ExcelTemplateParser
        using var stream = new MemoryStream(xlsxBytes);
        var parsedDoc = ExcelTemplateParser.Instance.Parse(stream);

        // 4. Verify parsed structure
        Assert.Single(parsedDoc.Pages);
        var page = parsedDoc.Pages[0];
        Assert.Equal("Sheet1", page.Name);

        // Verify A1:C1 merged cell
        var titleCell = page.FindCell(0, 0);
        Assert.NotNull(titleCell);
        Assert.Equal("Header Title", titleCell.Value);
        Assert.Equal(1, titleCell.RowSpan);
        Assert.Equal(3, titleCell.ColumnSpan);
        Assert.True(titleCell.Style?.Font?.Bold);
        Assert.Equal(HorizontalAlignment.Center, titleCell.Style?.HorizontalAlignment);

        // Verify row 2 cells
        var a2 = page.FindCell(1, 0);
        Assert.NotNull(a2);
        Assert.Equal("Product A", a2.Value);

        var b2 = page.FindCell(1, 1);
        Assert.NotNull(b2);
        Assert.Equal(10.0, Convert.ToDouble(b2.Value));

        var c2 = page.FindCell(1, 2);
        Assert.NotNull(c2);
        Assert.Equal(99.5, Convert.ToDouble(c2.Value));
    }

    [Fact]
    public void Render_TemplateDataBinding_ShouldSubstitutePlaceholdersAndRepeatingRows()
    {
        // 1. Create a template workbook in memory
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Invoice");

        // Row 1: Merged header
        ws.Range("A1:C1").Merge();
        ws.Cell("A1").Value = "发票 - {{Customer.Name}}";

        // Row 2: Metadata
        ws.Cell("A2").Value = "发票号: {{InvoiceNo}}";
        ws.Cell("C2").Value = "日期: {{Date}}";

        // Row 3: Table Header
        ws.Cell("A3").Value = "品名";
        ws.Cell("B3").Value = "数量";
        ws.Cell("C3").Value = "单价";

        // Row 4: Repeating Items Template Row
        ws.Cell("A4").Value = "{{Items.Name}}";
        ws.Cell("B4").Value = "{{Items.Qty}}";
        ws.Cell("C4").Value = "{{Items.Price}}";

        using var templateMs = new MemoryStream();
        workbook.SaveAs(templateMs);
        templateMs.Position = 0;

        // 2. Data model
        var data = new
        {
            Customer = new { Name = "Google Japan" },
            InvoiceNo = "INV-2026-001",
            Date = "2026-10-04",
            Items = new List<ItemDto>
            {
                new("Pixel 9 Pro", 2, 999m),
                new("Pixel Watch 3", 1, 349m),
                new("Pixel Buds Pro 2", 3, 229m)
            }
        };

        // 3. Render template with data
        var renderedDoc = ExcelTemplateParser.Instance.Render(templateMs, data);
        var page = renderedDoc.DefaultPage;

        // Verify scalar placeholder replacement
        Assert.Equal("发票 - Google Japan", page.FindCell(0, 0)?.Value);
        Assert.Equal("发票号: INV-2026-001", page.FindCell(1, 0)?.Value);
        Assert.Equal("日期: 2026-10-04", page.FindCell(1, 2)?.Value);

        // Verify table headers (row index 2)
        Assert.Equal("品名", page.FindCell(2, 0)?.Value);
        Assert.Equal("数量", page.FindCell(2, 1)?.Value);
        Assert.Equal("单价", page.FindCell(2, 2)?.Value);

        // Verify repeating rows (row index 3, 4, 5)
        // Item 1 (row 3)
        Assert.Equal("Pixel 9 Pro", page.FindCell(3, 0)?.Value);
        Assert.Equal("2", page.FindCell(3, 1)?.Value);
        Assert.Equal("999", page.FindCell(3, 2)?.Value);

        // Item 2 (row 4)
        Assert.Equal("Pixel Watch 3", page.FindCell(4, 0)?.Value);
        Assert.Equal("1", page.FindCell(4, 1)?.Value);
        Assert.Equal("349", page.FindCell(4, 2)?.Value);

        // Item 3 (row 5)
        Assert.Equal("Pixel Buds Pro 2", page.FindCell(5, 0)?.Value);
        Assert.Equal("3", page.FindCell(5, 1)?.Value);
        Assert.Equal("229", page.FindCell(5, 2)?.Value);
    }
}
