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
            var document = ReportService.Build(kind, data);
            int expected = kind is ReportKind.HotelReceipt or ReportKind.HotelInvoice ? 7 : 4;
            if (document.Pages.Count != expected) throw new InvalidOperationException("ページ数が一致しません。");
            if (document.Pages.SelectMany(p => p.Cells).Any(c => c.Value is string text && text.Contains("{{")))
                throw new InvalidOperationException("未解決のプレースホルダーがあります。");
            var printedNumbers = document.Pages.SelectMany(p => p.Cells)
                .Where(c => c.Column == 0 && c.ColumnSpan == 1)
                .Select(c => Convert.ToString(c.Value))
                .Where(s => int.TryParse(s, out _)).Select(s => int.Parse(s!)).ToArray();
            if (!printedNumbers.SequenceEqual(Enumerable.Range(1, 100)))
                throw new InvalidOperationException("明細の欠落または重複があります。");
            var layout = PrintSharp.Layout.GridLayoutEngine.Instance.CalculateDocument(document);
            if (layout.Pages.Any(p => p.Cells.Any(c => c.Bounds.Bottom > 818 || c.Bounds.Right > 572)))
                throw new InvalidOperationException("明細が A4 の印刷領域を超えています。");
            string excelPath = Path.Combine(directory, $"{kind}.xlsx");
            document.SaveAsExcel(excelPath);
            using var workbook = new XLWorkbook(excelPath);
            if (workbook.Worksheets.Count != expected) throw new InvalidOperationException("Excel シート数が一致しません。");
            using var pdf = document.ToPdfDocument();
            if (pdf.PageCount != expected) throw new InvalidOperationException("PDF ページ数が一致しません。");
            pdf.Save(Path.Combine(directory, $"{kind}.pdf"));
            using var print = document.ToPrintDocument();
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


