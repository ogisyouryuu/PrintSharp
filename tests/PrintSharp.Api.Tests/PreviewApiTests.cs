using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PrintSharp.Documents;
using PrintSharp.Pdf;
using Xunit;

namespace PrintSharp.Api.Tests;

public sealed class PreviewApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;
    public PreviewApiTests(WebApplicationFactory<Program> factory) => (_factory, _client) = (factory, factory.CreateClient());
    private static PrintRequest Request()
    {
        using var book = new XLWorkbook();
        book.AddWorksheet("Preview").Cell("A1").Value = "{{T.Value}}";
        using var stream = new MemoryStream(); book.SaveAs(stream);
        return new(new(Convert.ToBase64String(stream.ToArray())), AdapterTests.Input("decimal", "{\"Value\":1234.5}", format: "#,##0.00"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SnapshotCanBeReadCachedDownloadedAndDeleted(bool upload)
    {
        var request = Request();
        HttpResponseMessage created;
        if (upload)
        {
            using var body = new MultipartFormDataContent();
            body.Add(new ByteArrayContent(Convert.FromBase64String(request.Template.ContentBase64)), "template", "template.xlsx");
            body.Add(new StringContent(JsonSerializer.Serialize(request.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json"), "data");
            created = await _client.PostAsync("/api/v1/previews/upload", body);
        }
        else created = await _client.PostAsJsonAsync("/api/v1/previews", request);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var manifest = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, manifest.GetProperty("pageCount").GetInt32());
        string url = created.Headers.Location!.ToString();
        var response = await _client.GetAsync(url + "/pages/1");
        response.EnsureSuccessStatusCode();
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(manifest.GetProperty("revision").GetString(), page.GetProperty("revision").GetString());
        Assert.NotEmpty(page.GetProperty("resources").EnumerateObject());
        Assert.Equal("pt", page.GetProperty("page").GetProperty("unit").GetString());
        using var conditional = new HttpRequestMessage(HttpMethod.Get, url + "/pages/1");
        conditional.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await _client.SendAsync(conditional)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(url + "/pages/0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(url + "/pages/2")).StatusCode);
        var textResponse = await _client.GetAsync(url + "/text");
        textResponse.EnsureSuccessStatusCode();
        var textIndex = await textResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(manifest.GetProperty("revision").GetString(), textIndex.GetProperty("revision").GetString());
        var region = Assert.Single(textIndex.GetProperty("pages")[0].GetProperty("regions").EnumerateArray());
        Assert.Equal("1,234.50", region.GetProperty("text").GetString());
        Assert.NotEmpty(region.GetProperty("boxes").EnumerateArray());
        Assert.Equal(page.GetProperty("page").GetProperty("textRegions")[0].GetRawText(), region.GetRawText());
        var clip = page.GetProperty("page").GetProperty("commands").EnumerateArray().Single(c => c.GetProperty("op").GetString() == "clipRect").GetProperty("values");
        Assert.Equal(clip[0].GetSingle(), region.GetProperty("x").GetSingle());
        Assert.Equal(clip[1].GetSingle(), region.GetProperty("y").GetSingle());
        Assert.Equal(clip[2].GetSingle(), region.GetProperty("width").GetSingle());
        Assert.Equal(clip[3].GetSingle(), region.GetProperty("height").GetSingle());
        Assert.False(textIndex.TryGetProperty("resources", out _));
        using var textConditional = new HttpRequestMessage(HttpMethod.Get, url + "/text");
        textConditional.Headers.IfNoneMatch.Add(textResponse.Headers.ETag!);
        Assert.Equal(HttpStatusCode.NotModified, (await _client.SendAsync(textConditional)).StatusCode);
        var pdf = await _client.GetByteArrayAsync(url + "/pdf");
        using var parsed = UglyToad.PdfPig.PdfDocument.Open(pdf);
        Assert.Contains("1,234.50", parsed.GetPage(1).Text);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(url + "/text")).StatusCode);
    }

    [Fact]
    public async Task ApiPaddingConfigurationChangesTheSharedTextRegion()
    {
        using var configured = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<PrintApiOptions>(options => { options.TextPaddingHorizontal = 0; options.TextPaddingVertical = 0; })));
        using var zeroClient = configured.CreateClient();
        async Task<float[]> Clip(HttpClient client)
        {
            using var response = await client.PostAsJsonAsync("/api/v1/previews", Request());
            response.EnsureSuccessStatusCode();
            var page = await client.GetFromJsonAsync<JsonElement>(response.Headers.Location + "/pages/1");
            return page.GetProperty("page").GetProperty("commands").EnumerateArray()
                .Single(c => c.GetProperty("op").GetString() == "clipRect")
                .GetProperty("values").EnumerateArray().Select(v => v.GetSingle()).ToArray();
        }
        var zero = await Clip(zeroClient);
        var padded = await Clip(_client);
        Assert.Equal(zero[0] + 3, padded[0], 3);
        Assert.Equal(zero[1] + 2, padded[1], 3);
        Assert.Equal(zero[2] - 6, padded[2], 3);
        Assert.Equal(zero[3] - 4, padded[3], 3);
    }

    [Fact]
    public void StoreBoundsCapacityAndSeparatesOwners()
    {
        using var store = new PreviewStore(Options.Create(new PrintApiOptions { MaxPreviewSessions = 1 }));
        var document = SkiaPdfRenderer.Instance.CreatePreview(new Document());
        var alice = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "alice")], "test"));
        var bob = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "bob")], "test"));
        var entry = store.Add(document, alice, default);
        Assert.Equal(entry.Manifest.Length + entry.Pdf.Length + entry.Pages.Sum(p => p.Length) + entry.TextIndex.Length, entry.Size);
        Assert.Equal(404, Assert.Throws<PrintValidationException>(() => store.Get(entry.Id, bob)).Status);
        Assert.Equal(503, Assert.Throws<PrintValidationException>(() => store.Add(document, alice, default)).Status);
        store.Delete(entry.Id, alice);
        Assert.NotNull(store.Add(document, bob, default));
        using var tiny = new PreviewStore(Options.Create(new PrintApiOptions { MaxPreviewDocumentBytes = 1 }));
        Assert.Equal(413, Assert.Throws<PrintValidationException>(() => tiny.Add(document, alice, default)).Status);
    }
}
