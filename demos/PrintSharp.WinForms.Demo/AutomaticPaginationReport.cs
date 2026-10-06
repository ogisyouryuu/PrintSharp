using PrintSharp.Documents;
using PrintSharp.Fluent;
using BorderStyle = PrintSharp.Styles.BorderStyle;

namespace PrintSharp.WinForms.Demo;

/// <summary>独立的自动分页示例：仅声明报表区域和行高，不计算每页容量。</summary>
internal static class AutomaticPaginationReport
{
    public static Document Build(IReadOnlyList<StayRecord> records)
    {
        decimal grandTotal = records.Sum(item => item.Amount);
        return Document.Create(d => d.Title("宿泊一覧（自動改ページ）").Author("PrintSharp Demo")
            .Page("自動改ページ", page => page
                .Settings(s => s.PaperKind(PaperKind.A4).Orientation(PageOrientation.Portrait).Margins(24))
                .Columns(32, 96, 40, 80, 32, 76, 90)
                .Report(report => report
                    .Header(header =>
                    {
                        header.Rows(38, 24, 30, 26)
                            .Cell("A1:G1", c => c.Value("宿泊一覧（自動改ページ）").Font("Meiryo", 18, true))
                            .Cell("A2:G2", c => c.Value(new PageValue(context =>
                                $"{context.CurrentPageNumber} / {context.PageCount} ページ・本ページ {context.BodyRowCount} 件・全 {records.Count} 件"))
                                .Font("Meiryo", 10))
                            .Cell("A3:G3", c => c.Value("A4 縦・余白 24pt / 通常行 24pt・3泊以上の明細行 36pt")
                                .Font("Meiryo", 9));
                        string[] labels = ["No.", "宿泊者", "客室", "宿泊日", "泊数", "単価", "金額"];
                        for (int col = 0; col < labels.Length; col++)
                            header.Cell(3, col, c => c.Value(labels[col]).Font("Meiryo", 9, true)
                                .Background("#E8EEF6").BorderAll(BorderStyle.Thin));
                    })
                    .Body(body =>
                    {
                        foreach (var item in records)
                        {
                            // 明细行高度由报表定义；库据此决定换页位置。
                            bool longStay = item.Nights >= 3;
                            body.Row(longStay ? 36 : 24, row =>
                            {
                                object[] values = [item.Number, longStay ? item.Guest + "\n連泊プラン" : item.Guest,
                                    item.Room, item.Date.ToString("yyyy/MM/dd"), item.Nights,
                                    StayRecord.Yen(item.UnitPrice), StayRecord.Yen(item.Amount)];
                                for (int col = 0; col < values.Length; col++)
                                {
                                    int column = col;
                                    row.Cell(values[col], c =>
                                    {
                                        c.Font("Meiryo", 9).Padding(3).AlignMiddle().BorderAll(BorderStyle.Thin);
                                        if (column == 1) c.WrapText();
                                        if (column >= 4) c.AlignRight();
                                    });
                                }
                            });
                        }
                    })
                    .Footer(footer => footer.Rows(28, 24)
                        .Cell("A1:G1", c => c.Value(new PageValue(context =>
                            $"ページ小計：{StayRecord.Yen(records.Skip(context.BodyRowOffset).Take(context.BodyRowCount).Sum(item => item.Amount))} / 総合計：{StayRecord.Yen(grandTotal)}"))
                            .Font("Meiryo", 10, true))
                        .Cell("A2:G2", c => c.Value(new PageValue(context =>
                            context.CurrentPageNumber == context.PageCount
                                ? "以上 / 全明細の出力が完了しました。"
                                : "次の明細行が印刷領域に収まらないため、次ページに続きます。"))
                            .Font("Meiryo", 9))))));
    }
}
