using ClosedXML.Excel;
using PrintSharp.Documents;
using PrintSharp.Styles;
using Xunit;

namespace PrintSharp.Excel.Tests;

public class ClosedXmlRendererTests
{
    [Fact]
    public void RenderToWorkbook_ShouldProduceValidWorkbookWithMergedCellsAndStyles()
    {
        var doc = Document.Create(d =>
        {
            d.Title("Monthly Report")
             .Author("PrintSharp Team")
             .Page("Summary", p =>
             {
                 p.Columns(120f, 80f, 100f)
                  .Rows(30f, 25f, 25f)
                  .Cell("A1:C1", c =>
                  {
                      c.Value("销售汇总表")
                       .Font("Yu Gothic", 16, bold: true)
                       .AlignCenter()
                       .Background("#EEEEEE")
                       .BorderAll(BorderStyle.Medium, ColorSpec.Black);
                  })
                  .Cell("A2", c => c.Value("商品").Bold().AlignCenter())
                  .Cell("B2", c => c.Value("数量").Bold().AlignRight())
                  .Cell("C2", c => c.Value("金额").Bold().AlignRight())
                  .Cell("A3", "MacBook Pro")
                  .Cell("B3", c => c.Value(2).AlignRight())
                  .Cell("C3", c => c.Value(39998m).Format("¥#,##0.00").AlignRight())
                  .Cell("A4", "合计")
                  .Cell("B4", c => c.Formula("=SUM(B3:B3)").AlignRight())
                  .Cell("C4", c => c.Formula("=SUM(C3:C3)").Format("¥#,##0.00").AlignRight());
             });
        });

        using var workbook = doc.ToExcelWorkbook();
        Assert.NotNull(workbook);
        Assert.Equal("Monthly Report", workbook.Properties.Title);
        Assert.Equal("PrintSharp Team", workbook.Properties.Author);

        var ws = workbook.Worksheet("Summary");
        Assert.NotNull(ws);

        // Check merged range A1:C1
        Assert.Single(ws.MergedRanges);
        var mergedRange = ws.MergedRanges.First();
        Assert.Equal("A1:C1", mergedRange.RangeAddress.ToString());

        var cellA1 = ws.Cell("A1");
        Assert.Equal("销售汇总表", cellA1.GetString());
        Assert.True(cellA1.Style.Font.Bold);
        Assert.Equal(16.0, cellA1.Style.Font.FontSize);
        Assert.Equal(XLAlignmentHorizontalValues.Center, cellA1.Style.Alignment.Horizontal);

        // Check data row
        Assert.Equal("MacBook Pro", ws.Cell("A3").GetString());
        Assert.Equal(2.0, ws.Cell("B3").GetDouble());
        Assert.Equal(39998.0, ws.Cell("C3").GetDouble());
        Assert.Equal("¥#,##0.00", ws.Cell("C3").Style.NumberFormat.Format);

        // Check formula
        var cellB4 = ws.Cell("B4");
        Assert.True(cellB4.HasFormula);
        Assert.Equal("SUM(B3:B3)", cellB4.FormulaA1);
    }

    [Fact]
    public void ToExcelBytes_ShouldProduceValidXlsxPackage()
    {
        var doc = Document.Create(d =>
        {
            d.Cell("A1", "Hello Excel");
        });

        byte[] bytes = doc.ToExcelBytes();
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);

        // Re-read with ClosedXML to ensure file is a valid .xlsx
        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var ws = workbook.Worksheet(1);
        Assert.Equal("Hello Excel", ws.Cell("A1").GetString());
    }
}
