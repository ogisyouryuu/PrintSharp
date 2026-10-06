using System.Data;
using PrintSharp.Documents;
using PrintSharp.Excel;
using PrintSharp.Fluent;
using PrintSharp.Styles;
using BorderStyle = PrintSharp.Styles.BorderStyle;

namespace PrintSharp.WinForms.Demo;

public enum ReportKind { Code, GridTemplate, HotelReceipt, HotelInvoice, DataSet, AutomaticPagination }

public static class ReportService
{
    private static readonly PageSettings ReportSettings = new()
    {
        PaperKind = PaperKind.A4,
        Orientation = PageOrientation.Portrait,
        Margins = new PaddingSpec(24)
    };

    public static Document Build(ReportKind kind, IReadOnlyList<StayRecord> records)
    {
        if (records.Count == 0) throw new InvalidOperationException("请先输入至少一行数据。");
        return kind switch
        {
            ReportKind.Code => BuildCode(records),
            ReportKind.DataSet => BuildDataSetTemplate(records),
            ReportKind.AutomaticPagination => AutomaticPaginationReport.Build(records),
            _ => BuildTemplate(kind, records)
        };
    }

    private static PageValue PageLabel(int count) => new(context =>
        $"{context.CurrentPageNumber} / {context.PageCount} ページ・全 {count} 件");

    // 各明細は一行なので、ページの行範囲をそのまま元データの範囲として使用できる。
    private static decimal PageTotal(PageContext context, IReadOnlyList<StayRecord> records) =>
        records.Skip(context.BodyRowOffset).Take(context.BodyRowCount).Sum(x => x.Amount);

    private static Document BuildCode(IReadOnlyList<StayRecord> records)
    {
        return Document.Create(d => d.Title("宿泊一覧（コード）").Author("PrintSharp Demo")
            .Page("一覧", p => p
                .Settings(s => s.PaperKind(PaperKind.A4).Orientation(PageOrientation.Portrait).Margins(24))
                .Columns(32, 96, 40, 80, 32, 76, 90)
                .Report(report => report
                    .Header(h =>
                    {
                        h.Rows(38, 26, 26)
                         .Cell("A1:G1", c => c.Value("宿泊一覧（コード作成）").Font("Meiryo", 18, true))
                         .Cell("A2:G2", c => c.Value(PageLabel(records.Count)).Font("Meiryo", 10));
                        string[] headers = ["No.", "宿泊者", "客室", "宿泊日", "泊数", "単価", "金額"];
                        for (int col = 0; col < headers.Length; col++)
                            h.Cell(2, col, c => c.Value(headers[col]).Font("Meiryo", 9, true)
                                .Background("#E8EEF6").BorderAll(BorderStyle.Thin));
                    })
                    .Body(records, 24, (row, item) =>
                    {
                        object[] values = [item.Number, item.Guest, item.Room, item.Date.ToString("yyyy/MM/dd"),
                            item.Nights, StayRecord.Yen(item.UnitPrice), StayRecord.Yen(item.Amount)];
                        for (int col = 0; col < values.Length; col++)
                        {
                            int column = col;
                            row.Cell(values[col], c =>
                            {
                                c.Font("Meiryo", 9).Padding(3).BorderAll(BorderStyle.Thin);
                                if (column >= 4) c.AlignRight();
                            });
                        }
                    })
                    .Footer(f => f.Rows(30).Cell("A1:G1", c => c.Value(new PageValue(context =>
                        $"ページ小計：{StayRecord.Yen(PageTotal(context, records))} / 総合計：{StayRecord.Yen(records.Sum(x => x.Amount))}"))
                        .Font("Meiryo", 10, true))))));
    }

    private static Document BuildTemplate(ReportKind kind, IReadOnlyList<StayRecord> records)
    {
        TemplateFactory.EnsureTemplates();
        string name = kind switch
        {
            ReportKind.HotelReceipt => "HotelReceipt.xlsx",
            ReportKind.HotelInvoice => "HotelInvoice.xlsx",
            _ => "Grid.xlsx"
        };
        decimal total = records.Sum(x => x.Amount);
        var data = new
        {
            Items = records.Select(x => x.ToTemplateItem()).ToArray(),
            PageLabel = PageLabel(records.Count),
            Customer = "株式会社サンプル商事",
            DocumentNumber = $"DEMO-202610-{(kind == ReportKind.HotelReceipt ? "R" : "I")}",
            Period = $"宿泊期間：{records.Min(x => x.Date):yyyy/MM/dd} ～ {records.Max(x => x.Date.AddDays(x.Nights)):yyyy/MM/dd}",
            IssuedOn = "2026/10/31", DueOn = "2026/11/30",
            GrandTotal = StayRecord.Yen(total),
            PageTotal = new PageValue(context => StayRecord.Yen(PageTotal(context, records))),
            Tax = StayRecord.Yen(decimal.Floor(total * 10 / 110)),
            ClosingNote = new PageValue(context => context.CurrentPageNumber == context.PageCount
                ? "以上 / 本書はデモ用の架空帳票です。" : "明細は次ページに続きます。総額は全ページ共通です。")
        };
        var document = ExcelTemplateParser.Instance.RenderReport(
            Path.Combine(TemplateFactory.DirectoryPath, name), data, ReportSettings);
        document.Metadata.Title = Path.GetFileNameWithoutExtension(name);
        return document;
    }

    private static Document BuildDataSetTemplate(IReadOnlyList<StayRecord> records)
    {
        TemplateFactory.EnsureTemplates();
        var dataSet = CreateDataSet(records);
        var document = ExcelTemplateParser.Instance.RenderReport(
            Path.Combine(TemplateFactory.DirectoryPath, "DataSet.xlsx"), dataSet, ReportSettings);
        document.Metadata.Title = "宿泊一覧（DataSet データバインディング）";
        return document;
    }

    private static DataSet CreateDataSet(IReadOnlyList<StayRecord> records)
    {
        var dataSet = new DataSet("StayReport");
        var report = new DataTable("Report");
        report.Columns.Add("PageLabel", typeof(object));
        report.Columns.Add("PageTotal", typeof(object));
        report.Columns.Add("GrandTotal", typeof(decimal));
        report.Rows.Add(PageLabel(records.Count), new PageValue(context => PageTotal(context, records)),
            records.Sum(record => record.Amount));
        dataSet.Tables.Add(report);

        var items = new DataTable("Items");
        items.Columns.Add("Number", typeof(int));
        items.Columns.Add("Guest", typeof(string));
        items.Columns.Add("Room", typeof(string));
        items.Columns.Add("Date", typeof(DateTime));
        items.Columns.Add("Nights", typeof(int));
        items.Columns.Add("UnitPrice", typeof(decimal));
        items.Columns.Add("Amount", typeof(decimal));
        foreach (var record in records)
            items.Rows.Add(record.Number, record.Guest, record.Room, record.Date,
                record.Nights, record.UnitPrice, record.Amount);
        dataSet.Tables.Add(items);
        return dataSet;
    }
}
