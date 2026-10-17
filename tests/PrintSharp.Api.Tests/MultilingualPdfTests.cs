using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using PrintSharp.Pdf;
using Topten.RichTextKit;
using Xunit;

namespace PrintSharp.Api.Tests;

public sealed class MultilingualPdfTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public MultilingualPdfTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedMemoUsesBundledGlyphsAndShapesArabic(bool bold)
    {
        const string text = "中文欢迎 日本語ようこそ 한국어 환영합니다 العربية مَرْحَبًا لا ABC-123";
        var fonts = NotoFontRegistry.Default;
        var runs = fonts.ResolveRuns(text, "Arial", bold).ToArray();
        Assert.Equal(text, string.Concat(runs.Select(r => r.Text)));
        Assert.Contains(runs, r => r.Family == "NotoSansCJKjp");
        Assert.Contains(runs, r => r.Family == "NotoSansArabic");
        var block = new TextBlock { FontMapper = fonts, MaxWidth = 150 };
        foreach (var run in runs)
            block.AddText(run.Text, new Style { FontFamily = run.Family, FontWeight = bold ? 700 : 400 });
        Assert.True(block.Lines.Count > 1);
        Assert.Contains(block.FontRuns, r => r.Direction == TextDirection.RTL);
        Assert.Contains(block.FontRuns, r => r.Direction == TextDirection.LTR);
        foreach (var run in block.FontRuns)
            Assert.DoesNotContain((ushort)0, run.Glyphs.ToArray());

        // 文脈に応じたアラビア文字の字形が選択されることを確認します。
        var ligature = new TextBlock { FontMapper = fonts };
        var arabicStyle = new Style { FontFamily = "NotoSansArabic", FontWeight = bold ? 700 : 400 };
        ligature.AddText("لا", arabicStyle);
        var face = fonts.TypefaceFromStyle(arabicStyle, false);
        Assert.Contains(ligature.FontRuns.SelectMany(r => r.Glyphs.ToArray()),
            glyph => glyph != face.GetGlyph('ل') && glyph != face.GetGlyph('ا'));
    }

    [Fact]
    public void MissingFontsFailAtStartup()
    {
        Assert.Throws<FileNotFoundException>(() => new NotoFontRegistry(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void UnknownGlyphDoesNotSilentlyUseAnOsFont()
    {
        Assert.Throws<InvalidOperationException>(() => NotoFontRegistry.Default.ResolveRuns("\U0010FFFF", null, false).ToArray());
    }

    [Fact]
    public async Task ConcurrentMixedMemosProduceVectorPdfsWithEmbeddedFonts()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Memo");
        sheet.Cell("A1").Value = "{{T.Memo}}";
        sheet.Cell("A1").Style.Alignment.WrapText = true;
        sheet.Column(1).Width = 65;
        sheet.Row(1).Height = 100;
        using var template = new MemoryStream();
        workbook.SaveAs(template);
        using var client = _factory.CreateClient();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(async index =>
        {
            string memo = $"Memo {index}: 中文 日本語 한국어 العربية مَرْحَبًا لا (ABC-123)\n第二行：مرحبا بالعالم";
            var request = new PrintRequest(new(Convert.ToBase64String(template.ToArray())), new([
                new("T", [new("Memo", "string")], [JsonSerializer.SerializeToElement(new { Memo = memo })])
            ]));
            using var response = await client.PostAsJsonAsync("/api/v1/print/pdf", request);
            response.EnsureSuccessStatusCode();
            byte[] bytes = await response.Content.ReadAsByteArrayAsync();
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
            Assert.Equal(1, pdf.NumberOfPages);
            string content = Encoding.Latin1.GetString(bytes);
            Assert.Contains("NotoSansArabic", content);
            Assert.Contains("/FontFile2", content);
            Assert.Contains("/ToUnicode", content);
            Assert.DoesNotContain("/Subtype/Image", content.Replace(" ", ""));
        }));
    }
}
