using ClosedXML.Excel;

namespace PrintSharp.WinForms.Demo;

internal static class TemplateFactory
{
    public static string DirectoryPath => Path.Combine(AppContext.BaseDirectory, "Templates");

    public static void EnsureTemplates(string? directory = null)
    {
        directory ??= DirectoryPath;
        Directory.CreateDirectory(directory);
        Create(Path.Combine(directory, "Grid.xlsx"), false, false);
        Create(Path.Combine(directory, "HotelReceipt.xlsx"), true, false);
        Create(Path.Combine(directory, "HotelInvoice.xlsx"), true, true);
        CreateDataSet(Path.Combine(directory, "DataSet.xlsx"));
    }

    private static void CreateDataSet(string path)
    {
        if (File.Exists(path)) return;
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("DataSet");
        sheet.Range("A1:G7").Style.Font.FontName = "Meiryo";
        sheet.Range("A1:G7").Style.Font.FontSize = 9;
        sheet.Range("A1:G7").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        double[] widths = [5, 14, 6, 11, 5, 10, 12];
        for (int column = 1; column <= widths.Length; column++)
            sheet.Column(column).Width = widths[column - 1];

        Merge(sheet, "A1:G1", "宿泊一覧（DataSet データバインディング）", 17);
        sheet.Row(1).Height = 28;
        Merge(sheet, "A2:G2", "{{Report.PageLabel}}", 9);
        string[] headers = ["No.", "宿泊者", "客室", "宿泊日", "泊数", "単価", "金額"];
        string[] fields = ["Number", "Guest", "Room", "Date", "Nights", "UnitPrice", "Amount"];
        for (int column = 1; column <= headers.Length; column++)
        {
            sheet.Cell(4, column).Value = headers[column - 1];
            sheet.Cell(5, column).Value = "{{Items." + fields[column - 1] + "}}";
        }
        var table = sheet.Range("A4:G5");
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range("A4:G4").Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF6");
        sheet.Range("A4:G4").Style.Font.Bold = true;
        sheet.Cell("D5").Style.NumberFormat.Format = "yyyy/mm/dd";
        sheet.Range("F5:G5").Style.NumberFormat.Format = "¥#,##0";
        sheet.Range("E5:G5").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        Merge(sheet, "A7:G7", "本页小计：{{Report.PageTotal}}    全体合计：{{Report.GrandTotal}}", 10);
        sheet.Range("A7:G7").Style.NumberFormat.Format = "¥#,##0";
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.PagesWide = 1;
        sheet.PageSetup.PagesTall = 1;
        sheet.PageSetup.PrintAreas.Add("A1:G7");
        book.SaveAs(path);
    }

    // 既存テンプレートは上書きしないため、Excel で自由に編集できる。
    private static void Create(string path, bool hotel, bool invoice)
    {
        if (File.Exists(path)) return;
        using var book = new XLWorkbook();
        var sheet = book.AddWorksheet("Template");
        int lastRow = hotel ? 22 : 7;
        var area = sheet.Range($"A1:G{lastRow}");
        area.Style.Font.FontName = "Meiryo";
        area.Style.Font.FontSize = 9;
        area.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        double[] widths = [5, 14, 6, 11, 5, 10, 12];
        for (int c = 1; c <= widths.Length; c++) sheet.Column(c).Width = widths[c - 1];
        for (int r = 1; r <= lastRow; r++) sheet.Row(r).Height = 13.5;
        Merge(sheet, "A1:G1", hotel ? (invoice ? "請 求 書" : "領 収 書") : "宿泊一覧（Excel テンプレート）", 20);
        sheet.Row(1).Height = 30;
        Merge(sheet, "A2:G2", "{{PageLabel}}", 9);
        int header;
        if (hotel)
        {
            Merge(sheet, "A4:D4", "{{Customer}} 御中", 13);
            Merge(sheet, "E4:G4", "No. {{DocumentNumber}}", 9);
            Merge(sheet, "A5:D5", "{{Period}}", 9);
            Merge(sheet, "E5:G5", "発行日：{{IssuedOn}}", 9);
            Merge(sheet, "A7:G7", (invoice ? "ご請求金額：" : "領収金額：") + "{{GrandTotal}}（税込）", 17);
            sheet.Row(7).Height = 30;
            Merge(sheet, "A8:G8", invoice ? "下記のとおりご請求申し上げます。" : "但し、ご宿泊代として。上記正に領収いたしました。", 10);
            Merge(sheet, "A10:G10", "ホテル桜 東京（架空）  〒100-0001 東京都千代田区千代田1-1", 9);
            Merge(sheet, "A11:G11", "TEL：03-0000-0000  登録番号：T0000000000000（サンプル）", 9);
            header = 13;
        }
        else header = 4;
        string[] labels = ["No.", "宿泊者", "客室", "宿泊日", "泊数", "単価", "金額"];
        string[] fields = ["Number", "Guest", "Room", "Date", "Nights", "UnitPrice", "Amount"];
        for (int c = 1; c <= 7; c++)
        {
            sheet.Cell(header, c).Value = labels[c - 1];
            sheet.Cell(header + 1, c).Value = "{{Items." + fields[c - 1] + "}}";
        }
        var table = sheet.Range(header, 1, header + 1, 7);
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        sheet.Range(header, 1, header, 7).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8EEF6");
        sheet.Range(header, 1, header, 7).Style.Font.Bold = true;
        sheet.Range(header + 1, 5, header + 1, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        Merge(sheet, $"A{header + 3}:G{header + 3}", "ページ小計：{{PageTotal}}", 11);
        if (hotel)
        {
            Merge(sheet, $"A{header + 5}:G{header + 5}", "10%対象（税込）：{{GrandTotal}}  内消費税：{{Tax}}", 10);
            Merge(sheet, $"A{header + 7}:G{header + 7}", invoice
                ? "お支払期限：{{DueOn}} / 振込先：サンプル銀行 東京支店 普通 1234567"
                : "お支払方法：銀行振込 / 領収済み（デモ）", 9);
            Merge(sheet, $"A{header + 9}:G{header + 9}", "{{ClosingNote}}", 9);
        }
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.PagesWide = 1;
        sheet.PageSetup.PagesTall = 1;
        sheet.PageSetup.PrintAreas.Add($"A1:G{(hotel ? header + 9 : header + 3)}");
        book.SaveAs(path);
    }

    private static void Merge(IXLWorksheet sheet, string range, string value, double size)
    {
        var cells = sheet.Range(range).Merge();
        cells.FirstCell().Value = value;
        cells.Style.Font.FontSize = size;
    }
}

