using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PrintSharp.Documents;
using PrintSharp.Pdf;
using PrintSharp.Pdf.Preview;
using PrintSharp.Preview.AspNetCore;
using Xunit;
using Xunit.Abstractions;

namespace PrintSharp.Api.Tests;

public sealed class PreviewV2Tests(WebApplicationFactory<Program> factory, ITestOutputHelper output)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task SharedResourcesCompressionLegacyAndFilenameAreAvailable()
    {
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPrintPreviewStore>();
        var owner = new ClaimsPrincipal();
        var session = store.Create(Document.Create(d => d.Title("予約一覧").Page(p => p.Columns(200).Rows(30).Cell("A1", "日本語 123"))), owner);
        using var client = factory.CreateClient();
        string url = "/api/v1/previews/" + session.DocumentId;
        using var request = new HttpRequestMessage(HttpMethod.Get, url + "/pages/1?schemaVersion=2");
        request.Headers.AcceptEncoding.ParseAdd("gzip");
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        Assert.Contains("gzip", response.Content.Headers.ContentEncoding);
        Assert.Contains("Accept-Encoding", response.Headers.Vary);
        using var compressed = new GZipStream(await response.Content.ReadAsStreamAsync(), CompressionMode.Decompress);
        using var page = await JsonDocument.ParseAsync(compressed);
        Assert.Equal(2, page.RootElement.GetProperty("page").GetProperty("schemaVersion").GetInt32());
        Assert.False(page.RootElement.TryGetProperty("resources", out _));
        var ids = page.RootElement.GetProperty("resourceIds").EnumerateArray().Select(v => v.GetString()).ToArray();
        using var resources = JsonDocument.Parse(await client.GetStringAsync(url + "/resources?ids=" + string.Join(',', ids)));
        Assert.Equal(ids.Length, resources.RootElement.GetProperty("resources").EnumerateObject().Count());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url + "/resources?ids=missing")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(url + "/resources?ids=")).StatusCode);
        using var legacy = JsonDocument.Parse(await client.GetStringAsync(url + "/pages/1"));
        Assert.Equal(1, legacy.RootElement.GetProperty("page").GetProperty("schemaVersion").GetInt32());
        using var text = JsonDocument.Parse(await client.GetStringAsync(url + "/text?schemaVersion=2"));
        Assert.False(text.RootElement.GetProperty("pages")[0].GetProperty("regions")[0].TryGetProperty("boxes", out _));
        using var noGzip = new HttpRequestMessage(HttpMethod.Get, url + "/pages/1?schemaVersion=2");
        noGzip.Headers.AcceptEncoding.ParseAdd("gzip;q=0, *;q=1");
        Assert.Empty((await client.SendAsync(noGzip)).Content.Headers.ContentEncoding);
        var defaultPdf = await client.GetAsync(url + "/pdf");
        Assert.Equal("予約一覧.pdf", defaultPdf.Content.Headers.ContentDisposition!.FileNameStar);
        var namedPdf = await client.GetAsync(url + "/pdf?filename=" + Uri.EscapeDataString("../予約\\一覧\r\n.pdf"));
        Assert.Equal("予約一覧.pdf", namedPdf.Content.Headers.ContentDisposition!.FileNameStar);
        store.Delete(session.DocumentId, owner);
    }

    [Fact]
    public void NinePageReportTransfersLessThanOneThirdAndStoresCompressedBytes()
    {
        var document = Document.Create(d =>
        {
            for (int page = 1; page <= 9; page++)
                d.Page("Page" + page, p =>
                {
                    p.Settings(s => s.PaperKind(PaperKind.A4).Orientation(PageOrientation.Landscape).Margins(10));
                    p.Columns(Enumerable.Repeat(62f, 12).ToArray());
                    p.Rows(Enumerable.Repeat(22f, page == 9 ? 4 : 22).ToArray());
                    for (int row = 0; row < (page == 9 ? 4 : 22); row++)
                        for (int column = 0; column < 12; column++)
                            p.Cell(row, column, c => c.Text($"予約{row+1}受付").Font("NotoSansCJKjp", 8));
                });
        });
        var preview = SkiaPdfRenderer.Instance.CreatePreview(document);
        using var store = new PreviewStore(Options.Create(new PrintPreviewOptions()));
        var entry = store.Add(preview, new ClaimsPrincipal(), default);
        long baseline = entry.Manifest.Length + entry.TextIndex.Length + entry.Pages.Sum(p => (long)p.Length);
        long transferred = entry.ManifestBytes(true,true).Length + entry.Text(true,true).Length;
        var seen = new HashSet<string>();
        for (int page = 1; page <= 9; page++)
        {
            transferred += entry.Page(page,true,true).Length;
            using var json = JsonDocument.Parse(entry.Page(page,true));
            var missing = json.RootElement.GetProperty("resourceIds").EnumerateArray().Select(v => v.GetString()!).Where(id => seen.Add(id)).ToArray();
            foreach (var batch in missing.Chunk(64)) transferred += entry.Resources(batch,true).Length;
        }
        output.WriteLine($"Baseline JSON: {baseline}; v2 gzip transfer: {transferred}; old cache incl PDF: {baseline+entry.Pdf.Length}; compressed cache: {entry.Size}");
        Assert.True(transferred < baseline / 3);
        Assert.True(entry.Size - entry.Pdf.Length < baseline / 3);
        using var constrained = new PreviewStore(Options.Create(new PrintPreviewOptions { MaxDocumentBytes = entry.Size + 1024, MaxCacheBytes = (entry.Size + 1024) * 2, MaxSessions = 3 }));
        constrained.Add(preview,new ClaimsPrincipal(),default);
        constrained.Add(preview,new ClaimsPrincipal(),default);
        Assert.Equal(503, Assert.Throws<PreviewException>(() => constrained.Add(preview,new ClaimsPrincipal(),default)).Status);
    }
}
