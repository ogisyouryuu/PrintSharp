using ClosedXML.Excel;
using PrintSharp.Excel;

using var workbook = new XLWorkbook();
var worksheet = workbook.Worksheets.Add("AOT");
worksheet.Cell("A1").Value = "{{Customer.Name}}";
worksheet.Cell("A2").Value = "{{Items.Name}}";
using var templateStream = new MemoryStream();
workbook.SaveAs(templateStream);
templateStream.Position = 0;

var template = ExcelTemplate<AotInvoiceData>.Load(templateStream);
var document = template.Render(new AotInvoiceData
{
    Customer = new AotCustomerData { Name = "AOT Customer" },
    Items = [new AotItemData { Name = "AOT Item" }]
});

if (!Equals(document.DefaultPage.FindCell(0, 0)?.Value, "AOT Customer") ||
    !Equals(document.DefaultPage.FindCell(1, 0)?.Value, "AOT Item"))
{
    return 1;
}

Console.WriteLine("Generated Excel template bindings passed Native AOT smoke validation.");
return 0;

[GenerateTemplateBindings]
public sealed class AotInvoiceData
{
    public AotCustomerData? Customer { get; init; }
    public List<AotItemData> Items { get; init; } = new();
}

public sealed class AotCustomerData
{
    public string Name { get; init; } = string.Empty;
}

public sealed class AotItemData
{
    public string Name { get; init; } = string.Empty;
}
