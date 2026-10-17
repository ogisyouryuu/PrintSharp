using PrintSharp.Documents;
using PrintSharp.Fluent;
using BorderStyle = PrintSharp.Styles.BorderStyle;

namespace PrintSharp.WinForms.Demo;

/// <summary>一つのメモ欄に複数言語を保持したまま、同じ帳票を各形式に出力します。</summary>
internal static class MultilingualReport
{
    public static Document Build(IReadOnlyList<StayRecord> records) => Document.Create(d => d
        .Title("混合语言备注 / Multilingual memo").Author("PrintSharp Demo")
        .Page("多言語メモ", page => page
            .Settings(s => s.PaperKind(PaperKind.A4).Orientation(PageOrientation.Portrait).Margins(24))
            .Columns(32, 100, 415)
            .Report(report => report
                .Header(header => header.Rows(38, 24, 26)
                    .Cell("A1:C1", c => c.Text("混合语言备注 / Multilingual memo").Font("NotoSansCJKjp", 16, true))
                    .Cell("A2:C2", c => c.Value(new PageValue(context =>
                        $"{context.CurrentPageNumber} / {context.PageCount} ページ・全 {records.Count} 件 / 中文・日本語・한국어・العربية"))
                        .Font("NotoSansCJKjp", 9))
                    .Cell("A3", c => c.Text("No.").Font("NotoSansCJKjp", 9, true).Background("#E8EEF6").BorderAll(BorderStyle.Thin))
                    .Cell("B3", c => c.Text("宿泊者 / 客室").Font("NotoSansCJKjp", 9, true).Background("#E8EEF6").BorderAll(BorderStyle.Thin))
                    .Cell("C3", c => c.Text("Memo / 备注 / メモ / 메모 / ملاحظات").Font("NotoSansCJKjp", 9, true)
                        .Background("#E8EEF6").BorderAll(BorderStyle.Thin)))
                .Body(records, 78, (row, item) => row
                    .Cell(item.Number, c => c.Font("NotoSansCJKjp", 9).Padding(3).AlignTop().BorderAll(BorderStyle.Thin))
                    .Cell($"{item.Guest}\nRoom {item.Room}", c => c.Font("NotoSansCJKjp", 9).Padding(3).AlignTop().WrapText().BorderAll(BorderStyle.Thin))
                    .Cell(item.Memo, c => c.Font("NotoSansCJKjp", 9.5f).Padding(3).AlignTop().WrapText().BorderAll(BorderStyle.Thin)))
                .Footer(footer => footer.Rows(30).Cell("A1:C1", c => c.Text("Memo: 中文欢迎 / 日本語ようこそ / 한국어 환영합니다 / العربية مرحبًا بكم / ABC-123")
                    .Font("NotoSansCJKjp", 9))))));
}
