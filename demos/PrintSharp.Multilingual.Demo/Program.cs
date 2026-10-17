using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using PrintSharp.Api;

string output = Path.GetFullPath(args.Length > 0 ? args[0] : "output/pdf/multilingual");
Directory.CreateDirectory(output);

// 一つの memo に複数言語を保持し、サーバー側でフォントを自動選択します。
string[] memos =
[
    "中文：欢迎使用打印服务，预订成功。繁體中文：歡迎光臨，謝謝。",
    "日本語：ご予約ありがとうございます。領収書をご確認ください。",
    "한국어: 예약해 주셔서 감사합니다. 영수증을 확인해 주세요.",
    "العربية: مَرْحَبًا بِكُمْ، شُكْرًا لِحَجْزِكُمْ. رقم الحجز 12345، المبلغ 250.00 USD.",
    "Memo: 中文欢迎 / 日本語ようこそ / 한국어 환영합니다 / العربية مرحبًا بكم / English ABC-123.",
    "中文和العربية مَرْحَبًا、日本語と한국어 혼합. Invoice (INV-2026-001): 123.45 USD.\n第二行：لا إله إلا الله — السَّلَامُ عَلَيْكُمْ (Room 305).",
    "العربية أولاً: الحجز (ABC-123) بقيمة 250.00 USD؛ 中文确认、日本語確認、한국어 확인.",
    string.Concat(Enumerable.Repeat("混合换行：中文、日本語、한국어、العربية مرحبًا بكم، ABC-123. ", 4))
];

using var workbook = new XLWorkbook();
var sheet = workbook.AddWorksheet("Multilingual");
sheet.Column(1).Width = 5;
sheet.Column(2).Width = 90;
sheet.Style.Font.FontName = "NotoSansCJKjp";
sheet.Style.Font.FontSize = 11;
sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
sheet.Cell("A1").Value = "ID";
sheet.Cell("B1").Value = "Multilingual memo / 多语言备注 / 多言語 / 다국어 / العربية";
sheet.Row(1).Height = 30;
sheet.Range("A1:B1").Style.Fill.BackgroundColor = XLColor.FromHtml("#DCEAF5");
sheet.Range("A1:B1").Style.Font.Bold = true;
sheet.Cell("A2").Value = "{{Details.Id}}";
sheet.Cell("B2").Value = "{{Details.Memo}}";
sheet.Row(2).Height = 100;
sheet.Range("A2:B2").Style.Alignment.WrapText = true;
sheet.Range("A1:B2").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
sheet.Range("A1:B2").Style.Border.InsideBorder = XLBorderStyleValues.Thin;
sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
using var template = new MemoryStream();
workbook.SaveAs(template);
byte[] templateBytes = template.ToArray();
var data = new PrintDataSet([
    new("Details", [new("Id", "int"), new("Memo", "string")],
        memos.Select((memo, index) => JsonSerializer.SerializeToElement(new { Id = index + 1, Memo = memo })).ToList())
]);
await File.WriteAllBytesAsync(Path.Combine(output, "template.xlsx"), templateBytes);
await File.WriteAllTextAsync(Path.Combine(output, "data.json"), JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));

byte[] pdf;
if (args.Length > 1)
{
    // 起動済み API に同じ XLSX と JSON を multipart で送信します。
    using var client = new HttpClient { BaseAddress = new Uri(args[1]), Timeout = TimeSpan.FromMinutes(2) };
    using var form = new MultipartFormDataContent();
    var file = new ByteArrayContent(templateBytes);
    file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    form.Add(file, "template", "template.xlsx");
    form.Add(new StringContent(JsonSerializer.Serialize(data, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json"), "data");
    using var response = await client.PostAsync("api/v1/print/pdf/upload", form);
    if (!response.IsSuccessStatusCode) throw new InvalidOperationException(await response.Content.ReadAsStringAsync());
    pdf = await response.Content.ReadAsByteArrayAsync();
}
else
{
    // HTTP を介さず、API と同じ帳票生成処理を実行することもできます。
    using var service = new PrintGenerationService(Options.Create(new PrintApiOptions()));
    pdf = await service.GenerateAsync(templateBytes, data, true, CancellationToken.None);
}
await File.WriteAllBytesAsync(Path.Combine(output, "multilingual.pdf"), pdf);
await File.WriteAllTextAsync(Path.Combine(output, "environment.txt"), $"{RuntimeInformation.OSDescription}\n{RuntimeInformation.FrameworkDescription}\n{RuntimeInformation.ProcessArchitecture}\n");
Console.WriteLine($"Generated: {output} ({pdf.Length:N0} bytes)");
