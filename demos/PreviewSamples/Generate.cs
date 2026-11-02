#:package ClosedXML@0.105.1
using ClosedXML.Excel;

// リポジトリのルートから実行し、両デモで共有する二枚のテンプレートを再生成します。
var output = args.Length > 0 ? args[0] : Path.Combine("demos", "PreviewSamples");
Directory.CreateDirectory(output);
using var workbook = new XLWorkbook();
for (int number = 1; number <= 2; number++)
{
    var sheet = workbook.AddWorksheet($"Page {number}");
    sheet.Style.Font.FontName = "NotoSansCJKjp";
    sheet.Style.Font.FontSize = 11;
    sheet.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    sheet.Columns(1, 4).Width = 18;
    sheet.Rows(1, 14).Height = 28;
    sheet.Range("A1:D1").Merge().Value = number == 1 ? "PrintSharp · 多语言预览" : "PrintSharp · 第二页 / Page 2";
    sheet.Row(1).Height = 45;
    sheet.Range("A1:D1").Style.Font.FontSize = 20;
    sheet.Range("A1:D1").Style.Font.Bold = true;
    sheet.Range("A1:D1").Style.Fill.BackgroundColor = XLColor.FromHtml(number == 1 ? "#DCE9FC" : "#DDF1E6");
    sheet.Range("A3:D3").Merge().Value = "{{T.Value}}";
    sheet.Range("A3:D3").Style.Alignment.WrapText = true;
    sheet.Row(3).Height = 75;
    sheet.Cell("A5").Value = "项目 / Item";
    sheet.Range("B5:C5").Merge().Value = "说明 / Description";
    sheet.Cell("D5").Value = "金额 / Amount";
    sheet.Range("A5:D5").Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF2F7");
    for (int row = 6; row <= 10; row++)
    {
        sheet.Cell(row, 1).Value = $"{number}-{row - 5:00}";
        sheet.Range(row, 2, row, 3).Merge().Value = row % 2 == 0 ? "日本語・中文・한국어" : "العربية مرحبًا / English";
        sheet.Cell(row, 4).Value = (row - 5) * 1234.5;
        sheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";
    }
    sheet.Range("A5:D10").Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    sheet.Range("A5:D10").Style.Border.InsideBorder = XLBorderStyleValues.Thin;
    sheet.Range("A12:D12").Merge().Value = "Italic / underline / strike-through";
    sheet.Range("A12:D12").Style.Font.FontName = "NotoSans";
    sheet.Range("A12:D12").Style.Font.Italic = true;
    sheet.Range("A12:D12").Style.Font.Underline = XLFontUnderlineValues.Single;
    sheet.Range("A12:D12").Style.Font.Strikethrough = true;
    sheet.Range("A14:D14").Merge().Value = $"Page {number} / 2 · Canvas & PDF share one snapshot";
    sheet.Range("A14:D14").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
    sheet.PageSetup.Margins.Left = .25;
    sheet.PageSetup.Margins.Right = .25;
}
workbook.SaveAs(Path.Combine(output, "template.xlsx"));
File.WriteAllText(Path.Combine(output, "data.json"), """
{"tables":[{"name":"T","columns":[{"name":"Value","type":"string"}],"rows":[{"Value":"中文 日本語 한국어 العربية مرحبًا / Hello PrintSharp"}]}]}
""");
Console.WriteLine(Path.GetFullPath(output));
