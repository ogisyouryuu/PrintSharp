using ClosedXML.Excel;
using PrintSharp.Documents;
using PrintSharp.Fluent;
using PrintSharp.Styles;
using Xunit;

namespace PrintSharp.Excel.Tests;

public class ReportPaginationTests
{
    [Fact]
    public void ReportTemplate_ExportsPhysicalWorksheetsWithPageValues()
    {
        string path = Path.Combine(Path.GetTempPath(), $"PrintSharp-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.AddWorksheet("Report");
                sheet.Column(1).Width = 5;
                sheet.Row(1).Height = 7.5;
                sheet.Row(2).Height = 15;
                sheet.Row(3).Height = 7.5;
                sheet.Cell("A1").Value = "Page {{Label}}";
                sheet.Cell("A2").Value = "{{Items.Number}}";
                sheet.Cell("A3").Value = "Subtotal: {{Total}}";
                workbook.SaveAs(path);
            }
            var items = Enumerable.Range(1, 7).Select(n => new { Number = n }).ToArray();
            var data = new
            {
                Items = items,
                Label = new PageValue(c => $"{c.CurrentPageNumber}/{c.PageCount}"),
                Total = new PageValue(c => items.Skip(c.BodyRowOffset).Take(c.BodyRowCount).Sum(i => i.Number))
            };
            var document = ExcelTemplateParser.Instance.RenderReport(path, data,
                new PageSettings { Width = 100, Height = 100, Margins = new PaddingSpec(10) });
            Assert.Single(document.Pages);
            using var output = document.ToExcelWorkbook();
            Assert.Equal(3, output.Worksheets.Count);
            Assert.Equal("Page 1/3", output.Worksheet(1).Cell("A1").GetString());
            Assert.Equal("Subtotal: 6", output.Worksheet(1).Cell("A5").GetString());
            Assert.Equal("Subtotal: 15", output.Worksheet(2).Cell("A5").GetString());
            Assert.Equal("Page 3/3", output.Worksheet(3).Cell("A1").GetString());
            Assert.Equal("Subtotal: 7", output.Worksheet(3).Cell("A3").GetString());
            Assert.Equal(10d / 72, output.Worksheet(1).PageSetup.Margins.Top, 6);
            Assert.Equal(1, output.Worksheet(1).PageSetup.PagesTall);
            Assert.Equal(1, document.PageCount);
            // 原有 Render 不主动分页，且普通数据绑定仍可单独使用。
            var legacy = ExcelTemplateParser.Instance.Render(path, new { Items = items, Label = "All", Total = 28 });
            Assert.Null(legacy.DefaultPage.Pagination);
            using var legacyOutput = legacy.ToExcelWorkbook();
            Assert.Single(legacyOutput.Worksheets);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ReportTemplate_AllowsOneRowDataSetSummaryAndTypedDeferredValues()
    {
        string path = Path.Combine(Path.GetTempPath(), $"PrintSharp-{Guid.NewGuid():N}.xlsx");
        try
        {
            using (var book = new XLWorkbook())
            {
                var sheet = book.AddWorksheet("DataSet");
                sheet.Column(1).Width = 5;
                sheet.Cell("A1").Value = "{{Report.Label}}";
                sheet.Cell("A2").Value = "{{Items.Number}}";
                sheet.Cell("A3").Value = "{{Report.Total}}";
                book.SaveAs(path);
            }
            var data = new System.Data.DataSet();
            var summary = data.Tables.Add("Report");
            summary.Columns.Add("Label", typeof(object));
            summary.Columns.Add("Total", typeof(object));
            summary.Rows.Add(new PageValue(c => $"{c.CurrentPageNumber}/{c.PageCount}"),
                new PageValue(c => (decimal)c.BodyRowCount));
            var items = data.Tables.Add("Items");
            items.Columns.Add("Number", typeof(int));
            for (int n = 1; n <= 5; n++) items.Rows.Add(n);
            var doc = ExcelTemplateParser.Instance.RenderReport(path, data,
                new PageSettings { Width = 100, Height = 100, Margins = new PaddingSpec(10) });
            var physical = doc.Paginate();
            Assert.Equal(3, physical.PageCount);
            Assert.Equal("1/3", physical.DefaultPage.Cells[0].Value);
            Assert.Equal(2m, physical.DefaultPage.Cells.Last().Value);
            Assert.IsType<decimal>(physical.DefaultPage.Cells.Last().Value);
        }
        finally { File.Delete(path); }
    }
}
