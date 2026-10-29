using ClosedXML.Excel;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PrintSharp.WinForms.Demo;

internal static class MicroserviceDemoClient
{
    public const string DefaultEndpoint = "https://printsharp.ginbow.eu.org";
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromMinutes(2) };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<byte[]> GenerateAsync(string baseAddress, IReadOnlyList<StayRecord> records, bool pdf)
    {
        if (!Uri.TryCreate(NormalizeAddress(baseAddress), UriKind.Absolute, out var serviceUri) ||
            (serviceUri.Scheme != Uri.UriSchemeHttp && serviceUri.Scheme != Uri.UriSchemeHttps))
            throw new InvalidOperationException("请输入有效的 HTTP 或 HTTPS API 地址。");

        using var template = CreateTemplate();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(template.ToArray());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "template", "stay-report.xlsx");

        var data = CreateData(records);
        form.Add(new StringContent(JsonSerializer.Serialize(data, JsonOptions), Encoding.UTF8, "application/json"), "data");

        string format = pdf ? "pdf" : "excel";
        var endpoint = new Uri(serviceUri, $"api/v1/print/{format}/upload");
        using var response = await Client.PostAsync(endpoint, form);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"服务返回 HTTP {(int)response.StatusCode}: {detail}");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    private static string NormalizeAddress(string address)
    {
        string value = address.Trim();
        if (value.Length == 0) throw new InvalidOperationException("请输入 API 地址。");
        return value.EndsWith('/') ? value : value + "/";
    }

    private static object CreateData(IReadOnlyList<StayRecord> records)
    {
        var report = new ApiTable(
            "Report",
            [new("TotalAmount", "decimal", "#,##0", false)],
            [JsonSerializer.SerializeToElement(new { TotalAmount = records.Sum(record => record.Amount) })]);

        var items = new ApiTable(
            "Items",
            [
                new("Number", "int", null, false),
                new("Guest", "string", null, false),
                new("Room", "string", null, false),
                new("Date", "date", "yyyy/MM/dd", false),
                new("Nights", "int", null, false),
                new("UnitPrice", "decimal", "#,##0", false),
                new("Amount", "decimal", "#,##0", false)
            ],
            records.Select(record => JsonSerializer.SerializeToElement(new
            {
                record.Number,
                record.Guest,
                record.Room,
                Date = record.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                record.Nights,
                record.UnitPrice,
                Amount = record.Amount
            })).ToArray());

        return new { Tables = new[] { report, items } };
    }

    private static MemoryStream CreateTemplate()
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("StayReport");
            double[] widths = [7, 22, 11, 16, 9, 16, 16];
            for (int column = 1; column <= widths.Length; column++)
                sheet.Column(column).Width = widths[column - 1];

            sheet.Range("A1:G5").Style.Font.FontName = "NotoSansCJKjp";
            sheet.Range("A1:G5").Style.Font.FontSize = 10;
            sheet.Range("A1:G5").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            sheet.Range("A1:G1").Merge();
            sheet.Cell("A1").Value = "PrintSharp 微服务生成的住宿清单";
            sheet.Range("A1:G1").Style.Font.Bold = true;
            sheet.Range("A1:G1").Style.Font.FontSize = 16;
            sheet.Row(1).Height = 32;

            string[] headings = ["编号", "宿泊者", "客室", "宿泊日", "泊数", "单价（円）", "金额（円）"];
            string[] fields = ["Number", "Guest", "Room", "Date", "Nights", "UnitPrice", "Amount"];
            for (int column = 1; column <= headings.Length; column++)
            {
                sheet.Cell(2, column).Value = headings[column - 1];
                sheet.Cell(3, column).Value = "{{Items." + fields[column - 1] + "}}";
            }

            var table = sheet.Range("A2:G3");
            table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            sheet.Range("A2:G2").Style.Fill.BackgroundColor = XLColor.FromHtml("#DCEAF5");
            sheet.Range("A2:G2").Style.Font.Bold = true;
            sheet.Row(3).Height = 22;
            sheet.Cell("D3").Style.NumberFormat.Format = "yyyy/mm/dd";
            sheet.Range("F3:G3").Style.NumberFormat.Format = "#,##0";
            sheet.Range("E3:G3").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            sheet.Range("A5:B5").Merge();
            sheet.Cell("A5").Value = "合计（円）";
            sheet.Range("C5:G5").Merge();
            sheet.Cell("C5").Value = "{{Report.TotalAmount}}";
            sheet.Range("A5:G5").Style.Font.Bold = true;
            sheet.Range("C5:G5").Style.NumberFormat.Format = "#,##0";
            sheet.Range("C5:G5").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            sheet.Row(5).Height = 24;
            sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
            sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            sheet.PageSetup.PagesWide = 1;
            sheet.PageSetup.PagesTall = 0;
            sheet.PageSetup.PrintAreas.Add("A1:G5");
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return stream;
    }

    private sealed record ApiColumn(string Name, string Type, string? Format, bool Nullable);
    private sealed record ApiTable(string Name, IReadOnlyList<ApiColumn> Columns, IReadOnlyList<JsonElement> Rows);
}
