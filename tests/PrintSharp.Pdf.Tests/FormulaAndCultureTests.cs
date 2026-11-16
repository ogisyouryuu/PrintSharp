using System.Globalization;
using PrintSharp.Documents;
using PrintSharp.Fluent;
using Xunit;

namespace PrintSharp.Pdf.Tests;

public sealed class FormulaAndCultureTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public void BodySumPrintsResolvedSubtotalInPreviewAndPdf(int count)
    {
        var items = Enumerable.Range(1, count).ToArray();
        var document = Document.Create(d => d.Page(p => p.Settings(s => s.PaperKind(PaperKind.A4)).Columns(200)
            .Report(r => r.Header(h => h.Rows(20).Cell("A1", "Header"))
                .Body(items, 20, (row, item) => row.Cell(item))
                .Footer(f => f.Rows(20).Cell(0, 0, c => c.SumBodyColumn(0,
                    new PageValue(context => items.Skip(context.BodyRowOffset).Take(context.BodyRowCount).Sum())))))));
        var preview = SkiaPdfRenderer.Instance.CreatePreview(document);
        var pages = document.Paginate().Pages;
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(preview.ToPdfBytes());
        for (int i = 0; i < pages.Count; i++)
        {
            var context = pages[i].Context!;
            string expected = items.Skip(context.BodyRowOffset).Take(context.BodyRowCount).Sum().ToString(CultureInfo.InvariantCulture);
            Assert.Equal(expected, preview.Pages[i].TextRegions.Last().Text);
            Assert.Contains(expected, pdf.GetPage(i + 1).Text);
        }
    }

    [Fact]
    public void CachedFormulaProducesFormattedPreviewAndPdf()
    {
        var document = Document.Create(d => d.Page(p => p.Columns(250).Rows(30)
            .Cell(0, 0, c => c.Formula("=UNSUPPORTED(A2)", value: 6749600).Format("#,##0"))));
        var preview = SkiaPdfRenderer.Instance.CreatePreview(document);
        Assert.Equal("6,749,600", Assert.Single(preview.Pages[0].TextRegions).Text);
        using var pdf = UglyToad.PdfPig.PdfDocument.Open(preview.ToPdfBytes());
        Assert.Contains("6,749,600", pdf.GetPage(1).Text);
    }

    [Theory]
    [InlineData("SUM", "30")]
    [InlineData("AVERAGE", "15")]
    [InlineData("COUNT", "2")]
    [InlineData("MIN", "10")]
    [InlineData("MAX", "20")]
    public void AggregatesIgnoreBlankAndTextCells(string function, string expected)
    {
        var document = Document.Create(d => d.Page(p => p.Columns(200).Rows(25,25,25,25,25)
            .Cell("A1", 10).Cell("A2", 20).Cell("A3", "text")
            .Cell(4, 0, c => c.Formula($"={function}($A$1:A4)"))));
        Assert.Equal(expected, SkiaPdfRenderer.Instance.CreatePreview(document).Pages[0].TextRegions.Last().Text);
    }

    [Fact]
    public void UnsupportedAndCircularFormulasFollowFallback()
    {
        var document = Document.Create(d => d.Page(p => p.Columns(200).Rows(25)
            .Cell(0, 0, c => c.Formula("=SUM(A1:A1)"))));
        Assert.Empty(SkiaPdfRenderer.Instance.CreatePreview(document).Pages[0].TextRegions);
        Assert.Equal("SUM(A1:A1)", SkiaPdfRenderer.Instance.CreatePreview(document,
            new() { FormulaWithoutValue = FormulaFallback.Text }).Pages[0].TextRegions[0].Text);
        Assert.Contains("A1", Assert.Throws<InvalidOperationException>(() => SkiaPdfRenderer.Instance.CreatePreview(document,
            new() { FormulaWithoutValue = FormulaFallback.Throw })).Message);
    }

    [Theory]
    [InlineData("ko-KR")]
    [InlineData("de-DE")]
    public void CultureIsExplicitAndDefaultsToInvariant(string threadCulture)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(threadCulture);
            var document = Document.Create(d => d.Page(p => p.Columns(200).Rows(30,30)
                .Cell(0, 0, c => c.Value(new DateOnly(2026, 10, 20)).Format("yyyy/MM/dd"))
                .Cell(1, 0, c => c.Value(55000).Format("#,##0"))));
            var preview = SkiaPdfRenderer.Instance.CreatePreview(document);
            Assert.Equal(new[] { "2026/10/20", "55,000" }, preview.Pages[0].TextRegions.Select(r => r.Text));
            document.Culture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("55.000", SkiaPdfRenderer.Instance.CreatePreview(document).Pages[0].TextRegions[1].Text);
            Assert.Equal("55,000", SkiaPdfRenderer.Instance.CreatePreview(document,
                new() { Culture = CultureInfo.GetCultureInfo("ja-JP") }).Pages[0].TextRegions[1].Text);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public void DeferredFormulaCacheIsResolvedPerPhysicalPage()
    {
        var items = Enumerable.Range(1, 7).ToArray();
        var document = Document.Create(d => d.Page(p => p.Settings(s => s.Size(100,100).Margins(10)).Columns(60)
            .Report(r => r.Header(h => h.Rows(10).Cell("A1", "Header"))
                .Body(items, 20, (row, item) => row.Cell(item))
                .Footer(f => f.Rows(10).Cell(0,0,c => c.Formula("=SUM(A2:A8)",
                    new PageValue(context => items.Skip(context.BodyRowOffset).Take(context.BodyRowCount).Sum())))))));
        var preview = SkiaPdfRenderer.Instance.CreatePreview(document);
        Assert.Equal(new[] { "6", "15", "7" }, preview.Pages.Select(p => p.TextRegions.Last().Text));
    }

    [Fact]
    public void AutoDiscoveryLoadsOnlyInstalledFontsAndSuggestsMissingPackages()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (string weight in new[] { "Regular", "Bold" })
                File.Copy(Path.Combine(AppContext.BaseDirectory, "Fonts", $"NotoSans-{weight}.ttf"), Path.Combine(directory, $"NotoSans-{weight}.ttf"));
            using var registry = new NotoFontRegistry(directory);
            Assert.NotEmpty(registry.ResolveRuns("Latin 123", null, false));
            Assert.Contains("PrintSharp.Fonts.NotoSansCjk", Assert.Throws<InvalidOperationException>(() => registry.ResolveRuns("日本語", null, false).ToArray()).Message);
        }
        finally { Directory.Delete(directory, true); }
    }
}
