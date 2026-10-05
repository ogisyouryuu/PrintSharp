using PrintSharp.Documents;
using PrintSharp.Excel;
using PrintSharp.Fluent;
using PrintSharp.Styles;

namespace PrintSharp.WinForms.Demo;

public enum ReportKind { Code, GridTemplate, HotelReceipt, HotelInvoice }

public static class ReportService
{
    public static Document Build(ReportKind kind, IReadOnlyList<StayRecord> records)
    {
        if (records.Count == 0) throw new InvalidOperationException("请先输入至少一行数据。");
        return kind == ReportKind.Code ? BuildCode(records) : BuildTemplate(kind, records);
    }

    private static Document BuildCode(IReadOnlyList<StayRecord> records)
    {
        return Document.Create(d =>
        {
            d.Title("宿泊一覧（コード）").Author("PrintSharp Demo");
            var chunks = records.Chunk(25).ToArray();
            for (int index = 0; index < chunks.Length; index++)
            {
                int pageNumber = index + 1;
                var items = chunks[index];
                d.Page($"一覧-{pageNumber}", p =>
                {
                    p.Settings(s => s.Size(595.28f, 841.89f).Margins(24))
                     .Columns(32, 96, 40, 80, 32, 76, 90)
                     .Rows(38, 26, 26)
                     .Cell("A1:G1", c => c.Value("宿泊一覧（コード作成）").Font("Meiryo", 18, true))
                     .Cell("A2:G2", c => c.Value($"{pageNumber} / {chunks.Length} ページ・全 {records.Count} 件").Font("Meiryo", 10));
                    string[] headers = ["No.", "宿泊者", "客室", "宿泊日", "泊数", "単価", "金額"];
                    for (int col = 0; col < headers.Length; col++)
                        p.Cell(2, col, c => c.Value(headers[col]).Font("Meiryo", 9, true).Background("#E8EEF6").BorderAll(PrintSharp.Styles.BorderStyle.Thin));
                    for (int row = 0; row < items.Length; row++)
                    {
                        var item = items[row];
                        object[] values = [item.Number, item.Guest, item.Room, item.Date.ToString("yyyy/MM/dd"), item.Nights, StayRecord.Yen(item.UnitPrice), StayRecord.Yen(item.Amount)];
                        p.Row(row + 3, 24);
                        for (int col = 0; col < values.Length; col++)
                        {
                            int column = col;
                            p.Cell(row + 3, col, c =>
                            {
                                c.Value(values[column]).Font("Meiryo", 9).Padding(3).BorderAll(PrintSharp.Styles.BorderStyle.Thin);
                                if (column >= 4) c.AlignRight();
                            });
                        }
                    }
                    p.Row(items.Length + 3, 30)
                     .Cell(items.Length + 3, 0, 1, 7, c => c.Value($"ページ小計：{StayRecord.Yen(items.Sum(x => x.Amount))} / 総合計：{StayRecord.Yen(records.Sum(x => x.Amount))}").Font("Meiryo", 10, true));
                });
            }
        });
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
        bool hotel = kind is ReportKind.HotelReceipt or ReportKind.HotelInvoice;
        var chunks = records.Chunk(hotel ? 15 : 25).ToArray();
        var result = new Document();
        result.Pages.Clear();
        result.Metadata.Title = Path.GetFileNameWithoutExtension(name);
        decimal total = records.Sum(x => x.Amount);
        for (int index = 0; index < chunks.Length; index++)
        {
            var items = chunks[index];
            var data = new
            {
                Items = items.Select(x => x.ToTemplateItem()).ToArray(),
                PageLabel = $"{index + 1} / {chunks.Length} ページ・全 {records.Count} 件",
                Customer = "株式会社サンプル商事",
                DocumentNumber = $"DEMO-202610-{(kind == ReportKind.HotelReceipt ? "R" : "I")}",
                Period = $"宿泊期間：{records.Min(x => x.Date):yyyy/MM/dd} ～ {records.Max(x => x.Date.AddDays(x.Nights)):yyyy/MM/dd}",
                IssuedOn = "2026/10/31", DueOn = "2026/11/30",
                GrandTotal = StayRecord.Yen(total), PageTotal = StayRecord.Yen(items.Sum(x => x.Amount)),
                Tax = StayRecord.Yen(decimal.Floor(total * 10 / 110)),
                ClosingNote = index == chunks.Length - 1 ? "以上 / 本書はデモ用の架空帳票です。" : "明細は次ページに続きます。総額は全ページ共通です。"
            };
            // 一つのテンプレートをページごとのデータに適用し、共通 Document に集約する。
            var rendered = ExcelTemplateParser.Instance.Render(Path.Combine(TemplateFactory.DirectoryPath, name), data);
            foreach (var page in rendered.Pages)
            {
                page.PageNumber = result.Pages.Count + 1;
                page.Name = $"明細-{page.PageNumber}";
                page.Settings = page.Settings with { Width = 595.28f, Height = 841.89f, Margins = new PaddingSpec(24) };
                result.Pages.Add(page);
            }
        }
        return result;
    }
}

