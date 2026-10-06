using ClosedXML.Excel;
using PrintSharp.Excel;
using PrintSharp.Pdf;
using PrintSharp.Windows;
using System.Drawing.Printing;

namespace PrintSharp.WinForms.Demo;

internal static class SmokeTest
{
    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        using var form = new MainForm();
        form.CreateControl();
        var data = StayRecord.CreateSamples();
        foreach (var kind in Enum.GetValues<ReportKind>())
        {
            var logical = ReportService.Build(kind, data);
            var document = PrintSharp.Layout.DocumentPaginator.Instance.Paginate(logical);
            int expected = document.PageCount;
            if (logical.PageCount != 1 || logical.DefaultPage.Pagination is null)
                throw new InvalidOperationException("デモは一つの論理報告書を定義する必要があります。");
            if (document.Pages.SelectMany(p => p.Cells).Any(c => c.Value is string text && text.Contains("{{")))
                throw new InvalidOperationException("未解決のプレースホルダーがあります。");
            var printedNumbers = document.Pages.SelectMany(p => p.Cells)
                .Where(c => c.Column == 0 && c.ColumnSpan == 1)
                .Select(c => Convert.ToString(c.Value))
                .Where(s => int.TryParse(s, out _)).Select(s => int.Parse(s!)).ToArray();
            if (!printedNumbers.SequenceEqual(Enumerable.Range(1, 100)))
                throw new InvalidOperationException("明細の欠落または重複があります。");
            var layout = PrintSharp.Layout.GridLayoutEngine.Instance.CalculateDocument(document);
            if (layout.Pages.Any(p => p.ContentHeight + p.Page.Settings.Margins.Top + p.Page.Settings.Margins.Bottom > p.TotalHeight + 0.01f ||
                p.Cells.Any(c => c.Bounds.Right > p.TotalWidth - p.Page.Settings.Margins.Right + 0.01f)))
                throw new InvalidOperationException("明細が A4 の印刷領域を超えています。");
            foreach (var page in layout.Pages.SkipLast(1))
            {
                var context = page.Page.Context!;
                int nextRow = context.SourcePage.Pagination!.HeaderRowCount + context.BodyRowOffset + context.BodyRowCount;
                float nextHeight = context.SourcePage.Rows.FirstOrDefault(r => r.Index == nextRow)?.Height
                    ?? context.SourcePage.Settings.DefaultRowHeight;
                float remaining = page.TotalHeight - page.Page.Settings.Margins.Top - page.Page.Settings.Margins.Bottom - page.ContentHeight;
                if (remaining + 0.01f >= nextHeight)
                    throw new InvalidOperationException("次の明細行を配置できるのに改ページしています。");
            }
            if (kind == ReportKind.AutomaticPagination)
            {
                foreach (var page in document.Pages)
                {
                    var context = page.Context!;
                    string label = $"{context.CurrentPageNumber} / {expected} ページ・本ページ {context.BodyRowCount} 件・全 {data.Count} 件";
                    if (!page.Cells.Any(c => Equals(c.Value, label)))
                        throw new InvalidOperationException("自動改ページのページ情報が一致しません。");
                    decimal subtotal = data.Skip(context.BodyRowOffset).Take(context.BodyRowCount).Sum(r => r.Amount);
                    string total = $"ページ小計：{StayRecord.Yen(subtotal)} / 総合計：{StayRecord.Yen(data.Sum(r => r.Amount))}";
                    if (!page.Cells.Any(c => Equals(c.Value, total)))
                        throw new InvalidOperationException("自動改ページのページ小計が一致しません。");
                }
                var heights = logical.DefaultPage.Rows.Skip(4).Take(data.Count).Select(r => r.Height).Distinct().ToArray();
                if (heights.Length != 2 || !heights.Contains(24) || !heights.Contains(36))
                    throw new InvalidOperationException("異なる明細行高の例が作成されていません。");
            }
            string excelPath = Path.Combine(directory, $"{kind}.xlsx");
            logical.SaveAsExcel(excelPath);
            using var workbook = new XLWorkbook(excelPath);
            if (workbook.Worksheets.Count != expected) throw new InvalidOperationException("Excel シート数が一致しません。");
            using var pdf = logical.ToPdfDocument();
            if (pdf.PageCount != expected) throw new InvalidOperationException("PDF ページ数が一致しません。");
            pdf.Save(Path.Combine(directory, $"{kind}.pdf"));
            using var print = logical.ToPrintDocument();
            if (print.PrinterSettings.IsValid)
            {
                print.DefaultPageSettings.PaperSize = new PaperSize("A4", 827, 1169);
                var preview = new PreviewPrintController();
                print.PrintController = preview;
                print.Print();
                var pages = preview.GetPreviewPageInfo();
                try
                {
                    using var bitmap = new Bitmap(1240, 1754);
                    using (var graphics = Graphics.FromImage(bitmap))
                    {
                        graphics.Clear(Color.White);
                        graphics.DrawImage(pages[0].Image, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    }
                    bitmap.Save(Path.Combine(directory, $"{kind}-preview.png"), System.Drawing.Imaging.ImageFormat.Png);
                    if (pages.Length != expected) throw new InvalidOperationException("印刷プレビューページ数が一致しません。");
                }
                finally { foreach (var page in pages) page.Image.Dispose(); }
            }
        }
        TemplateFactory.EnsureTemplates(Path.Combine(directory, "Templates"));
        File.WriteAllText(Path.Combine(directory, "success.txt"), "Excel / PDF / Windows preview smoke test passed.");
    }
}


