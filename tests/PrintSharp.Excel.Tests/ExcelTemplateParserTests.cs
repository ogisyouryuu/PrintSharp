using System.Collections;
using System.Data;
using ClosedXML.Excel;
using PrintSharp.Documents;
using PrintSharp.Excel;
using PrintSharp.Pdf;
using PdfSharp.Fonts;
using PrintSharp.Styles;
using Xunit;

namespace PrintSharp.Excel.Tests;

[GenerateTemplateBindings]
public sealed class GeneratedInvoiceData
{
    public GeneratedCustomerData? Customer { get; init; }
    public decimal Total { get; init; }
    public List<GeneratedItemData> Items { get; init; } = new();
}

public sealed class GeneratedCustomerData
{
    public string Name { get; init; } = string.Empty;
    public string Address { get; init; } = string.Empty;
}

public sealed class GeneratedItemData
{
    public string Name { get; init; } = string.Empty;
}

public class ExcelTemplateParserTests
{
    private record ItemDto(string Name, int Qty, decimal Price);
    private sealed class ResolverRoot
    {
        public ResolverCustomer? Customer { get; init; }
        public string? MissingValue { get; init; }
        public string PublicField = "field-value";
    }

    private sealed class ResolverCustomer
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class FixedValueResolver : ITemplateValueResolver
    {
        public object? Resolve(object? data, string path) => path switch
        {
            "Name" => "Generated Name",
            _ => null
        };
    }

    [Fact]
    public void ReflectionValueResolver_ShouldResolvePocoNestedPropertiesFieldsAndCaseInsensitivePaths()
    {
        var resolver = new ReflectionValueResolver();
        var data = new ResolverRoot { Customer = new ResolverCustomer { Name = "Taro" } };

        Assert.Equal("Taro", resolver.Resolve(data, "customer.name"));
        Assert.Equal("field-value", resolver.Resolve(data, "publicfield"));
        Assert.Null(resolver.Resolve(data, "Customer.Missing"));
        Assert.Null(resolver.Resolve(new ResolverRoot(), "Customer.Name"));
        Assert.Null(resolver.Resolve(data, "Missing"));
    }

    [Fact]
    public void DictionaryValueResolver_ShouldResolveGenericAndNonGenericDictionaries()
    {
        var resolver = new DictionaryValueResolver();
        var genericData = new Dictionary<string, object?>
        {
            ["Customer"] = new Dictionary<string, object?> { ["Name"] = "Taro" },
            ["NullValue"] = null
        };
        IDictionary nonGenericData = new Hashtable { ["Customer"] = new Hashtable { ["Name"] = "Hanako" } };

        Assert.Equal("Taro", resolver.Resolve(genericData, "Customer.Name"));
        Assert.Equal("Hanako", resolver.Resolve(nonGenericData, "Customer.Name"));
        Assert.Null(resolver.Resolve(genericData, "Customer.Missing"));
        Assert.Null(resolver.Resolve(genericData, "NullValue"));
    }

    [Fact]
    public void ExcelTemplateParser_ShouldAcceptCustomResolver()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Sheet1").Cell("A1").Value = "{{Name}}";
        using var template = new MemoryStream();
        workbook.SaveAs(template);
        template.Position = 0;

        var parser = new ExcelTemplateParser(new FixedValueResolver());
        var document = parser.Render(template, new object());

        Assert.Equal("Generated Name", document.DefaultPage.FindCell(0, 0)?.Value);
    }

    [Fact]
    public void ExcelTemplateParser_ShouldBindNestedDictionariesAndDictionaryCollectionRows()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        worksheet.Cell("A1").Value = "{{Customer.Name}}";
        worksheet.Cell("A2").Value = "{{Items.Name}}";
        using var template = new MemoryStream();
        workbook.SaveAs(template);
        template.Position = 0;

        var data = new Dictionary<string, object?>
        {
            ["Customer"] = new Dictionary<string, object?> { ["Name"] = "Taro" },
            ["Items"] = new[]
            {
                new Dictionary<string, object?> { ["Name"] = "One" },
                new Dictionary<string, object?> { ["Name"] = "Two" }
            }
        };

        var document = ExcelTemplateParser.Instance.Render(template, data);

        Assert.Equal("Taro", document.DefaultPage.FindCell(0, 0)?.Value);
        Assert.Equal("One", document.DefaultPage.FindCell(1, 0)?.Value);
        Assert.Equal("Two", document.DefaultPage.FindCell(2, 0)?.Value);
    }

    [Fact]
    public void GeneratedTemplateResolver_ShouldUseDirectAccessForRootNestedAndCollectionValues()
    {
        var resolver = new GeneratedInvoiceDataTemplateValueResolver();
        var data = new GeneratedInvoiceData
        {
            Customer = new GeneratedCustomerData { Name = "Taro", Address = "Tokyo" },
            Total = 12.5m,
            Items = new List<GeneratedItemData> { new() { Name = "Item A" } }
        };

        Assert.Equal("Taro", resolver.Resolve(data, "customer.name"));
        Assert.Equal("Tokyo", resolver.Resolve(data, "Customer.Address"));
        Assert.Equal(12.5m, resolver.Resolve(data, "Total"));
        Assert.Equal(data.Items, resolver.Resolve(data, "Items"));
        Assert.Equal("Item A", resolver.Resolve(data.Items[0], "Name"));
        Assert.Null(resolver.Resolve(new GeneratedInvoiceData(), "Customer.Name"));
    }

    [Fact]
    public void ExcelTemplateParser_ShouldRenderWithGeneratedTemplateResolver()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        worksheet.Cell("A1").Value = "{{Customer.Name}}";
        worksheet.Cell("B1").Value = "住所：{{Customer.Address}}";
        worksheet.Cell("A2").Value = "{{Items.Name}}";
        worksheet.Cell("B2").Value = "{{Total}}";
        using var template = new MemoryStream();
        workbook.SaveAs(template);
        template.Position = 0;

        var data = new GeneratedInvoiceData
        {
            Customer = new GeneratedCustomerData { Name = "Taro", Address = "Tokyo" },
            Total = 12.5m,
            Items = new List<GeneratedItemData> { new() { Name = "Item A" }, new() { Name = "Item B" } }
        };
        var parser = new ExcelTemplateParser(new GeneratedInvoiceDataTemplateValueResolver());

        var document = parser.Render(template, data);

        Assert.Equal("Taro", document.DefaultPage.FindCell(0, 0)?.Value);
        Assert.Equal("住所：Tokyo", document.DefaultPage.FindCell(0, 1)?.Value);
        Assert.Equal("Item A", document.DefaultPage.FindCell(1, 0)?.Value);
        Assert.Equal(12.5m, document.DefaultPage.FindCell(1, 1)?.Value);
        Assert.Equal("Item B", document.DefaultPage.FindCell(2, 0)?.Value);
    }

    [Fact]
    public void ExcelTemplateOfT_ShouldUseRegisteredGeneratedResolverAutomatically()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        worksheet.Cell("A1").Value = "{{Customer.Name}}";
        worksheet.Cell("A2").Value = "{{Items.Name}}";
        using var templateStream = new MemoryStream();
        workbook.SaveAs(templateStream);
        templateStream.Position = 0;

        var data = new GeneratedInvoiceData
        {
            Customer = new GeneratedCustomerData { Name = "Hanako" },
            Items = new List<GeneratedItemData> { new() { Name = "Generated Item" } }
        };
        var template = ExcelTemplate<GeneratedInvoiceData>.Load(templateStream);

        var document = template.Render(data);

        Assert.IsType<GeneratedInvoiceDataTemplateValueResolver>(TemplateValueResolverRegistry<GeneratedInvoiceData>.Resolver);
        Assert.Equal("Hanako", document.DefaultPage.FindCell(0, 0)?.Value);
        Assert.Equal("Generated Item", document.DefaultPage.FindCell(1, 0)?.Value);
    }

    [Fact]
    public void ExcelTemplateOfT_ShouldReuseCompiledTemplateAcrossRenderCalls()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Sheet1").Cell("A1").Value = "{{Customer.Name}}";
        using var templateStream = new MemoryStream();
        workbook.SaveAs(templateStream);
        templateStream.Position = 0;

        var template = ExcelTemplate<GeneratedInvoiceData>.Load(templateStream);
        var first = template.Render(new GeneratedInvoiceData
        {
            Customer = new GeneratedCustomerData { Name = "First" }
        });
        var second = template.Render(new GeneratedInvoiceData
        {
            Customer = new GeneratedCustomerData { Name = "Second" }
        });

        Assert.Equal("First", first.DefaultPage.FindCell(0, 0)?.Value);
        Assert.Equal("Second", second.DefaultPage.FindCell(0, 0)?.Value);
    }

    [Fact]
    public void DataSetResolver_ShouldResolveTablesRowsAndPreserveClrTypes()
    {
        var dataSet = CreateDataSet();
        var resolver = new DataSetTemplateValueResolver();

        Assert.Equal("Taro", resolver.Resolve(dataSet, "Customer.Name"));
        Assert.Equal("Tokyo", resolver.Resolve(dataSet.Tables["Customer"]!.Rows[0], "Address"));
        Assert.Equal(100m, resolver.Resolve(dataSet, "Items.Price"));
        Assert.IsType<decimal>(resolver.Resolve(dataSet, "Items.Price"));
        Assert.IsType<int>(resolver.Resolve(dataSet, "Items.Quantity"));
        Assert.IsType<DateTime>(resolver.Resolve(dataSet, "Items.CreatedAt"));
        Assert.Null(resolver.Resolve(dataSet.Tables["Customer"]!.Rows[0], "Optional"));
        Assert.Null(resolver.Resolve(dataSet, "Missing.Name"));
        Assert.Null(resolver.Resolve(dataSet, "Items.Missing"));
        Assert.Null(resolver.Resolve(dataSet, "items.Price"));
    }

    [Fact]
    public void DataTableAndDataRow_ShouldResolveDirectly()
    {
        var table = CreateDataSet().Tables["Customer"]!;
        var resolver = new DataSetTemplateValueResolver();

        Assert.Equal("Taro", resolver.Resolve(table, "Name"));
        Assert.Equal("Taro", resolver.Resolve(table.Rows[0], "Name"));

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");
        worksheet.Cell("A1").Value = "{{Name}}";
        worksheet.Cell("A2").Value = "{{Items.Name}}";
        using var template = new MemoryStream();
        workbook.SaveAs(template);
        template.Position = 0;
        var tableDocument = new ExcelTemplateParser().Render(template, CreateDataSet().Tables["Items"]!);
        Assert.Equal("Apple", tableDocument.DefaultPage.FindCell(0, 0)?.Value);
        Assert.Equal("Apple", tableDocument.DefaultPage.FindCell(1, 0)?.Value);
        Assert.Equal("Orange", tableDocument.DefaultPage.FindCell(2, 0)?.Value);

        template.Position = 0;
        var rowDocument = new ExcelTemplateParser().Render(template, table.Rows[0]);
        Assert.Equal("Taro", rowDocument.DefaultPage.FindCell(0, 0)?.Value);
    }

    [Fact]
    public void Render_DataSet_ShouldExpandTableRowsAndSupportMixedSources()
    {
        var dataSet = CreateDataSet();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Cell("A1").Value = "{{Customer.Name}}";
        sheet.Cell("A2").Value = "{{Items.Name}}";
        sheet.Cell("B2").Value = "{{Items.Price}}";
        sheet.Cell("C2").Value = "{{Items.Quantity}}";
        sheet.Cell("D2").Value = "{{Items.CreatedAt}}";
        using var template = new MemoryStream();
        workbook.SaveAs(template);
        template.Position = 0;

        var document = new ExcelTemplateParser().Render(template, dataSet);

        Assert.Equal("Taro", document.DefaultPage.FindCell(0, 0)?.Value);
        Assert.Equal("Apple", document.DefaultPage.FindCell(1, 0)?.Value);
        Assert.Equal(100m, document.DefaultPage.FindCell(1, 1)?.Value);
        Assert.Equal(2, document.DefaultPage.FindCell(1, 2)?.Value);
        Assert.Equal(new DateTime(2026, 1, 1), document.DefaultPage.FindCell(1, 3)?.Value);
        Assert.Equal("Orange", document.DefaultPage.FindCell(2, 0)?.Value);
        Assert.Equal(200m, document.DefaultPage.FindCell(2, 1)?.Value);
        Assert.Equal(3, document.DefaultPage.FindCell(2, 2)?.Value);
        Assert.Equal(new DateTime(2026, 1, 2), document.DefaultPage.FindCell(2, 3)?.Value);

        var mixed = new Dictionary<string, object?>
        {
            ["Customer"] = dataSet.Tables["Customer"]!.Rows[0],
            ["Items"] = dataSet.Tables["Items"]!
        };
        template.Position = 0;
        var mixedDocument = new ExcelTemplateParser().Render(template, mixed);
        Assert.Equal("Taro", mixedDocument.DefaultPage.FindCell(0, 0)?.Value);
        Assert.Equal("Apple", mixedDocument.DefaultPage.FindCell(1, 0)?.Value);
        Assert.Equal(200m, mixedDocument.DefaultPage.FindCell(2, 1)?.Value);
    }

    [Fact]
    public void Render_DataSetDocument_ShouldExportToExcelAndPdf()
    {
        var dataSet = CreateDataSet();
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sheet1");
        sheet.Style.Font.FontName = "Arial";
        sheet.Cell("A1").Value = "{{Items.Name}}";
        sheet.Cell("B1").Value = "{{Items.Price}}";
        using var template = new MemoryStream();
        workbook.SaveAs(template);
        template.Position = 0;

        var document = new ExcelTemplateParser().Render(template, dataSet);
        using var excel = new MemoryStream();
        document.SaveAsExcel(excel);
        excel.Position = 0;
        using var renderedWorkbook = new XLWorkbook(excel);
        Assert.Equal("Orange", renderedWorkbook.Worksheet(1).Cell("A2").GetString());
        Assert.Equal(200d, renderedWorkbook.Worksheet(1).Cell("B2").GetDouble());

        byte[] pdf;
        if (OperatingSystem.IsWindows())
        {
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
            pdf = document.ToPdfBytes();
        }
        else
        {
            using var pdfTemplateWorkbook = new XLWorkbook();
            pdfTemplateWorkbook.Worksheets.Add("Sheet1").Cell("A1").Value = "{{Customer.Optional}}";
            using var pdfTemplate = new MemoryStream();
            pdfTemplateWorkbook.SaveAs(pdfTemplate);
            pdfTemplate.Position = 0;
            var pdfDocument = new ExcelTemplateParser().Render(pdfTemplate, dataSet);
            Assert.Null(pdfDocument.DefaultPage.FindCell(0, 0)?.Value);
            pdf = pdfDocument.ToPdfBytes();
        }
        Assert.True(pdf.Length > 4);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));
    }

    private static DataSet CreateDataSet()
    {
        var dataSet = new DataSet("Store");
        var customer = new DataTable("Customer");
        customer.Columns.Add("Name", typeof(string));
        customer.Columns.Add("Address", typeof(string));
        customer.Columns.Add("Optional", typeof(string));
        customer.Rows.Add("Taro", "Tokyo", DBNull.Value);
        dataSet.Tables.Add(customer);

        var items = new DataTable("Items");
        items.Columns.Add("Name", typeof(string));
        items.Columns.Add("Price", typeof(decimal));
        items.Columns.Add("Quantity", typeof(int));
        items.Columns.Add("CreatedAt", typeof(DateTime));
        items.Rows.Add("Apple", 100m, 2, new DateTime(2026, 1, 1));
        items.Rows.Add("Orange", 200m, 3, new DateTime(2026, 1, 2));
        dataSet.Tables.Add(items);
        return dataSet;
    }

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
    public void Render_ShouldPreserveSolidRgbAndThemeCellBackgrounds()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Colors");
        worksheet.Cell("A1").Value = "{{Title}}";
        worksheet.Cell("A1").Style.Fill.BackgroundColor = XLColor.FromHtml("#AABBCC");
        worksheet.Cell("B1").Value = "Theme";
        worksheet.Cell("B1").Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1, 0.25);
        using var template = new MemoryStream();
        workbook.SaveAs(template);
        template.Position = 0;

        var document = new ExcelTemplateParser().Render(template, new Dictionary<string, object?>
        {
            ["Title"] = "Report"
        });

        Assert.Equal(ColorSpec.FromHex("#AABBCC"), document.DefaultPage.FindCell(0, 0)?.Style?.BackColor);
        var themeBase = workbook.Theme.ResolveThemeColor(XLThemeColor.Accent1).Color;
        var expectedThemeColor = System.Drawing.Color.FromArgb(
            themeBase.A,
            TintComponent(themeBase.R, 0.25),
            TintComponent(themeBase.G, 0.25),
            TintComponent(themeBase.B, 0.25));
        Assert.Equal(ColorSpec.FromRgba(expectedThemeColor.R, expectedThemeColor.G, expectedThemeColor.B, expectedThemeColor.A),
            document.DefaultPage.FindCell(0, 1)?.Style?.BackColor);

        using var exported = new MemoryStream(document.ToExcelBytes());
        using var renderedWorkbook = new XLWorkbook(exported);
        var exportedColor = renderedWorkbook.Worksheet(1).Cell("A1").Style.Fill.BackgroundColor.Color;
        Assert.Equal(ColorSpec.FromHex("#AABBCC"),
            ColorSpec.FromRgba(exportedColor.R, exportedColor.G, exportedColor.B, exportedColor.A));
    }

    private static int TintComponent(byte value, double tint) =>
        (int)Math.Round(value * (1 - tint) + 255 * tint, MidpointRounding.AwayFromZero);

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
        Assert.Equal(2, page.FindCell(3, 1)?.Value);
        Assert.Equal(999m, page.FindCell(3, 2)?.Value);

        // Item 2 (row 4)
        Assert.Equal("Pixel Watch 3", page.FindCell(4, 0)?.Value);
        Assert.Equal(1, page.FindCell(4, 1)?.Value);
        Assert.Equal(349m, page.FindCell(4, 2)?.Value);

        // Item 3 (row 5)
        Assert.Equal("Pixel Buds Pro 2", page.FindCell(5, 0)?.Value);
        Assert.Equal(3, page.FindCell(5, 1)?.Value);
        Assert.Equal(229m, page.FindCell(5, 2)?.Value);
    }
}
