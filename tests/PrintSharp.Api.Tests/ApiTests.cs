using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace PrintSharp.Api.Tests;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ApiTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private static PrintRequest Request(string value = "Test", string? templateFormat = null, string? columnFormat = null)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Report");
        sheet.Cell("A1").Value = "{{T.Value}}";
        if (templateFormat is not null)
            sheet.Cell("A1").Style.NumberFormat.Format = templateFormat;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new(new(Convert.ToBase64String(stream.ToArray()), "../../unsafe\r\n.xlsx"),
            AdapterTests.Input(columnFormat is null ? "string" : "decimal", JsonSerializer.Serialize(new { Value = columnFormat is null ? (object)value : 12.34m }), format: columnFormat));
    }

    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task GeneratesRealFiles(string route, string mime)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/print/" + route, Request());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mime, response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("document.", response.Content.Headers.ContentDisposition!.FileName);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (route == "pdf")
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes));
        else
        {
            using var workbook = new XLWorkbook(new MemoryStream(bytes));
            Assert.Equal("Test", workbook.Worksheet(1).Cell("A1").GetString());
        }
    }

    [Theory]
    [InlineData(null, "#,##0.00")]
    [InlineData("0.0", "0.0")]
    public async Task ColumnFormatIsDefaultAndTemplateWins(string? templateFormat, string expected)
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/print/excel", Request(templateFormat: templateFormat, columnFormat: "#,##0.00"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        Assert.Equal(expected, workbook.Worksheet(1).Cell("A1").Style.NumberFormat.Format);
        Assert.Equal(12.34, workbook.Worksheet(1).Cell("A1").GetDouble());
    }

    [Theory]
    [InlineData("{", 400, "invalid_json")]
    [InlineData("null", 400, "required")]
    [InlineData("{}", 400, "required")]
    [InlineData("{\"template\":{\"contentBase64\":\"!!!\"},\"data\":{\"tables\":[]}}", 400, "invalid_base64")]
    [InlineData("{\"template\":{\"contentBase64\":\"AQI=\"},\"data\":{\"tables\":[]}}", 400, "invalid_template")]
    public async Task ErrorsUseProblemJson(string json, int status, string code)
    {
        var response = await _factory.CreateClient().PostAsync("/api/v1/print/excel", new StringContent(json, Encoding.UTF8, "application/json"));
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.True(body.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task LimitsMediaAndUnknownRoutes()
    {
        using var limited = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<PrintApiOptions>(o => o.MaxRequestBytes = 32)));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await limited.CreateClient().PostAsJsonAsync("/api/v1/print/pdf", Request())).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await _factory.CreateClient().PostAsync("/api/v1/print/pdf", new StringContent("{}"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _factory.CreateClient().PostAsJsonAsync("/api/v1/print/csv", Request())).StatusCode);
        Assert.Equal("ok", (await _factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/health")).GetProperty("status").GetString());
    }

    [Fact]
    public async Task ConcurrentRequestsKeepIndependentData()
    {
        using var client = _factory.CreateClient();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(async index =>
        {
            var response = await client.PostAsJsonAsync("/api/v1/print/excel", Request("Value" + index));
            response.EnsureSuccessStatusCode();
            using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
            Assert.Equal("Value" + index, workbook.Worksheet(1).Cell("A1").GetString());
        }));
    }

    private sealed class FailingService : IPrintGenerationService
    {
        public Task<byte[]> GenerateAsync(byte[] template, PrintDataSet data, bool pdf, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("secret server path C:/secret");
        public Task<byte[]> GenerateAsync(PrintRequest request, bool pdf, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("secret server path C:/secret");
    }

    [Fact]
    public async Task ServerFailureDoesNotLeakDetails()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.AddSingleton<IPrintGenerationService, FailingService>()));
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/print/pdf", Request());
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.Contains("generation_failed", text);
        Assert.DoesNotContain("secret", text);
        Assert.DoesNotContain("InvalidOperationException", text);
    }

    [Fact]
    public async Task CancelledServiceReleasesSlot()
    {
        using var service = new PrintGenerationService(Options.Create(new PrintApiOptions { MaxConcurrency = 1 }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GenerateAsync(Request(), false, new(true)));
        Assert.NotEmpty(await service.GenerateAsync(Request(), false, CancellationToken.None));
    }

    [Fact]
    public async Task MultipleTablesExpandRowsAndEmptyTableIsSupported()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Report");
        sheet.Cell("A1").Value = "{{Header.Title}}";
        sheet.Cell("A2").Value = "{{Details.Quantity}}";
        sheet.Cell("B2").Value = "{{Details.Price}}";
        sheet.Cell("A3").Value = "{{Empty.Name}}";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var request = new PrintRequest(new(Convert.ToBase64String(stream.ToArray())), new([
            new("Header", [new("Title", "string")], [JsonSerializer.SerializeToElement(new { Title = "Invoice" })]),
            new("Details", [new("Quantity", "int"), new("Price", "decimal", "0.00")],
                [JsonSerializer.SerializeToElement(new { Quantity = 1, Price = 2.5m }), JsonSerializer.SerializeToElement(new { Quantity = 2, Price = 3m })]),
            new("Empty", [new("Name", "string")], [])]));
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/print/excel", request);
        response.EnsureSuccessStatusCode();
        using var output = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        Assert.Equal("Invoice", output.Worksheet(1).Cell("A1").GetString());
        Assert.Equal(1, output.Worksheet(1).Cell("A2").GetDouble());
        Assert.Equal(2, output.Worksheet(1).Cell("A3").GetDouble());
        Assert.Equal("0.00", output.Worksheet(1).Cell("B3").Style.NumberFormat.Format);
    }

    [Theory]
    [InlineData("template")]
    [InlineData("expanded")]
    [InlineData("sheets")]
    [InlineData("cells")]
    [InlineData("output")]
    public async Task TemplateResourceLimitsReturn413(string limit)
    {
        var request = Request();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<PrintApiOptions>(o =>
            {
                if (limit == "template")
                    o.MaxTemplateBytes = 1;
                if (limit == "expanded")
                    o.MaxExpandedBytes = 1;
                if (limit == "sheets")
                    o.MaxZipEntries = 1;
                if (limit == "cells")
                    o.MaxTemplateCells = 1;
                if (limit == "output")
                    o.MaxGeneratedCells = 1;
            })));
        if (limit is "cells" or "output")
        {
            using var workbook = new XLWorkbook();
            workbook.AddWorksheet("Report").Cell("B2").Value = "{{T.Value}}";
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            request = request with { Template = new(Convert.ToBase64String(stream.ToArray())) };
        }
        var response = await factory.CreateClient().PostAsJsonAsync("/api/v1/print/excel", request);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    private sealed class ChunkedContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }

    [Fact]
    public async Task ChunkedRequestCannotBypassBodyLimit()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<PrintApiOptions>(o => o.MaxRequestBytes = 32)));
        using var content = new ChunkedContent(JsonSerializer.SerializeToUtf8Bytes(Request()));
        content.Headers.ContentType = new("application/json");
        var response = await factory.CreateClient().PostAsync("/api/v1/print/excel", content);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task UnknownTemplateBindingReturnsSafe400()
    {
        var request = Request() with { Data = new([]) };
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/print/excel", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid_binding", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FormattedPdfIsReadable()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/print/pdf", Request(columnFormat: "#,##0.00"));
        response.EnsureSuccessStatusCode();
        using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(await response.Content.ReadAsByteArrayAsync()), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount > 0);
    }
}
