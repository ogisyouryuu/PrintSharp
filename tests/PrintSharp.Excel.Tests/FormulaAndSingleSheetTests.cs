using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;
using PrintSharp.Documents;
using PrintSharp.Fluent;
using Xunit;

namespace PrintSharp.Excel.Tests;

public sealed class FormulaAndSingleSheetTests
{
    [Theory]
    [InlineData(0, 1, ExcelReportLayout.SingleSheet)]
    [InlineData(50, 1, ExcelReportLayout.SingleSheet)]
    [InlineData(50, 2, ExcelReportLayout.SingleSheet)]
    [InlineData(0, 2, ExcelReportLayout.PerPhysicalPage)]
    [InlineData(50, 2, ExcelReportLayout.PerPhysicalPage)]
    public void BodySumUsesOutputRowsAndSurvivesRecalculation(int count, int headerRows, ExcelReportLayout layout)
    {
        var items = Enumerable.Range(1, count).ToArray();
        var doc = Document.Create(d => d.Page(p => p.Settings(s => s.PaperKind(PaperKind.A4)).Columns(80, 80)
            .Report(r => r.Header(h => h.Rows(Enumerable.Repeat(20f, headerRows).ToArray()).Cell("A1", "Header"))
                .Body(items, 20, (row, item) => { row.Cell(item); row.Cell(item * 1000); })
                .Footer(f => f.Rows(20).Cell(0, 1, c => c.SumBodyColumn(1,
                    new PageValue(context => items.Skip(context.BodyRowOffset).Take(context.BodyRowCount).Sum(x => x * 1000))))))));

        using var book = new XLWorkbook(new MemoryStream(doc.ToExcelBytes(new() { ReportLayout = layout })));
        int offset = 0;
        foreach (var sheet in book.Worksheets)
        {
            int last = sheet.LastRowUsed()!.RowNumber();
            int bodyCount = last - headerRows - 1;
            var total = sheet.Cell(last, 2);
            int expected = items.Skip(offset).Take(bodyCount).Sum(x => x * 1000);
            Assert.Equal(bodyCount == 0 ? "0" : $"SUM(B{headerRows + 1}:B{last - 1})", total.FormulaA1);
            Assert.Equal(expected, total.CachedValue.GetNumber());
            sheet.RecalculateAllFormulas();
            Assert.Equal(expected, total.GetDouble());
            offset += bodyCount;
        }
        Assert.Equal(count, offset);
        Assert.IsType<PageValue>(doc.Pages[0].Cells.Last().Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public void ContinuousContextCanSuppressPageSubtotal(int count)
    {
        var doc = Document.Create(d => d.Page(p => p.Settings(s => s.PaperKind(PaperKind.A4)).Columns(100)
            .Report(r => r.Header(h => h.Rows(20).Cell("A1", "Header"))
                .Body(Enumerable.Range(1, count), 20, (row, item) => row.Cell(item))
                .Footer(f => f.Rows(20).Cell(0, 0, c => c.Value(new PageValue(context =>
                    context.IsContinuous ? "" : $"Page {context.BodyRowCount}")))))));
        using var continuous = doc.ToExcelWorkbook(new() { ReportLayout = ExcelReportLayout.SingleSheet });
        Assert.True(continuous.Worksheet(1).Cell(count + 2, 1).IsEmpty());
        using var physical = doc.ToExcelWorkbook();
        foreach (var sheet in physical.Worksheets)
            Assert.StartsWith("Page ", sheet.LastRowUsed()!.Cell(1).GetString());
        Assert.All(doc.Paginate().Pages, page => Assert.False(page.Context!.IsContinuous));
    }

    [Theory]
    [InlineData(1234.5, "n", "1234.5")]
    [InlineData("日本語", "str", "日本語")]
    [InlineData(true, "b", "1")]
    public void ExcelContainsRealFormulaAndExplicitCache(object value, string type, string expected)
    {
        var doc = Document.Create(d => d.Page(p => p.Columns(100).Rows(25)
            .Cell(0, 0, c => c.Formula("=UNSUPPORTED(A2)", value))));
        byte[] bytes = doc.ToExcelBytes();
        using var archive = new ZipArchive(new MemoryStream(bytes));
        using var input = archive.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(input); XNamespace ns = xml.Root!.Name.Namespace;
        var cell = xml.Descendants(ns + "c").Single();
        Assert.Equal("UNSUPPORTED(A2)", cell.Element(ns + "f")!.Value);
        Assert.Equal(type, cell.Attribute("t")!.Value);
        Assert.Equal(expected, cell.Element(ns + "v")!.Value);
        using var book = new XLWorkbook(new MemoryStream(bytes));
        Assert.False(book.Worksheet(1).Cell("A1").CachedValue.IsBlank);
    }

    [Fact]
    public void SingleSheetRetainsAllRowsAndPrintTitlesAndResolvesWholeBodySubtotal()
    {
        var items = Enumerable.Range(1, 60).ToArray();
        var doc = Document.Create(d => d.Culture("ja-JP").Page("Report", p => p.Settings(s => s.PaperKind(PaperKind.A4)).Columns(100,100)
            .Report(r => r.Header(h => h.Rows(20).Cell("A1", "Header"))
                .Body(items, 20, (row, value) => row.DefaultStyle(style => style.Bold().Format("0.00")).Cell(value))
                .Footer(f => f.Rows(20).Cell(0,0,c => c.Formula("=SUM(A2:A61)",
                    new PageValue(context => items.Skip(context.BodyRowOffset).Take(context.BodyRowCount).Sum())))
                    .Cell(0,1,c => c.Value(PageValue.PageNumbers))))));
        using var book = doc.ToExcelWorkbook(new() { ReportLayout = ExcelReportLayout.SingleSheet, FreezeHeaderRows = true });
        var sheet = Assert.Single(book.Worksheets);
        Assert.Equal(62, sheet.LastRowUsed()!.RowNumber());
        for (int i = 1; i <= 60; i++) Assert.Equal(i, sheet.Cell(i+1,1).GetDouble());
        Assert.True(sheet.Cell("A2").Style.Font.Bold);
        Assert.Equal("0.00", sheet.Cell("A2").Style.NumberFormat.Format);
        Assert.Equal(1830, sheet.Cell("A62").CachedValue.GetNumber());
        Assert.Equal(1, sheet.PageSetup.FirstRowToRepeatAtTop);
        Assert.Equal(1, sheet.PageSetup.LastRowToRepeatAtTop);
        Assert.Equal(1, sheet.SheetView.SplitRow);
        Assert.Contains("&P / &N", sheet.PageSetup.Footer.Center.GetText(XLHFOccurrence.OddPages));
        Assert.Equal(0, sheet.PageSetup.PagesTall);
        using var physical = doc.ToExcelWorkbook();
        Assert.True(physical.Worksheets.Count > 1);
        Assert.Equal("ja-JP", doc.Paginate().Culture!.Name);
    }
}
