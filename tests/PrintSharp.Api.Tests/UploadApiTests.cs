using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PrintSharp.Api;
using Xunit;

namespace PrintSharp.Api.Tests;

public class UploadApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private static byte[] Template(string? format = null)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Report");
        sheet.Cell("A1").Value = "{{T.Value}}";
        if (format is not null) sheet.Cell("A1").Style.NumberFormat.Format = format;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static PrintDataSet Data(string value = "Upload") =>
        AdapterTests.Input("string", JsonSerializer.Serialize(new { Value = value }));

    private static MultipartFormDataContent Form(byte[]? template = null, string? data = null, string filename = "template.xlsx")
    {
        var form = new MultipartFormDataContent();
        if (template is not null)
        {
            var content = new ByteArrayContent(template);
            // A misleading extension/MIME must not bypass actual XLSX validation.
            content.Headers.ContentType = new("application/octet-stream");
            form.Add(content, "template", filename);
        }
        if (data is not null) form.Add(new StringContent(data, Encoding.UTF8, "application/json"), "data");
        return form;
    }

    private static async Task AssertError(HttpResponseMessage response, int status, string code, string? path = null)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("errors")[0].GetProperty("code").GetString());
        if (path is not null) Assert.Equal(path, body.GetProperty("errors")[0].GetProperty("path").GetString());
    }

    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("excel", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task UploadProducesRealFiles(string route, string mime)
    {
        using var form = Form(Template(), JsonSerializer.Serialize(Data()), "../../untrusted.bin");
        var response = await factory.CreateClient().PostAsync($"/api/v1/print/{route}/upload", form);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(mime, response.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain("untrusted", response.Content.Headers.ContentDisposition!.ToString());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        if (route == "pdf")
        {
            Assert.StartsWith("%PDF-", Encoding.ASCII.GetString(bytes));
            using var pdf = UglyToad.PdfPig.PdfDocument.Open(bytes);
            Assert.True(pdf.NumberOfPages > 0);
        }
        else
        {
            using var workbook = new XLWorkbook(new MemoryStream(bytes));
            Assert.Equal("Upload", workbook.Worksheet(1).Cell("A1").GetString());
        }
    }

    [Theory]
    [InlineData(null, "0.00")]
    [InlineData("0.0", "0.0")]
    public async Task UploadAndBase64ShareFormatting(string? templateFormat, string expected)
    {
        var template = Template(templateFormat);
        var data = AdapterTests.Input("decimal", "{\"Value\":12.34}", format: "0.00");
        using var form = Form(template, JsonSerializer.Serialize(data));
        var client = factory.CreateClient();
        var upload = await client.PostAsync("/api/v1/print/excel/upload", form);
        var original = await client.PostAsJsonAsync("/api/v1/print/excel", new PrintRequest(new(Convert.ToBase64String(template)), data));
        upload.EnsureSuccessStatusCode();
        original.EnsureSuccessStatusCode();
        using var uploadedWorkbook = new XLWorkbook(new MemoryStream(await upload.Content.ReadAsByteArrayAsync()));
        using var originalWorkbook = new XLWorkbook(new MemoryStream(await original.Content.ReadAsByteArrayAsync()));
        var cell = uploadedWorkbook.Worksheet(1).Cell("A1");
        Assert.Equal(12.34, cell.GetDouble());
        Assert.Equal(expected, cell.Style.NumberFormat.Format);
        Assert.Equal(originalWorkbook.Worksheet(1).Cell("A1").GetFormattedString(), cell.GetFormattedString());
    }

    [Theory]
    [InlineData("missing_template", "required", "template")]
    [InlineData("empty_template", "required", "template")]
    [InlineData("missing_data", "required", "data")]
    [InlineData("null_data", "required", "data")]
    [InlineData("invalid_json", "invalid_json", "data")]
    [InlineData("invalid_template", "invalid_template", "template")]
    [InlineData("invalid_data", "required", "data.tables")]
    [InlineData("invalid_binding", "invalid_binding", "template")]
    [InlineData("duplicate_template", "invalid_multipart", "request")]
    [InlineData("duplicate_data", "invalid_multipart", "request")]
    [InlineData("unknown_part", "invalid_multipart", "request")]
    [InlineData("template_not_file", "required", "template")]
    public async Task UploadErrorsAreConsistent(string scenario, string code, string path)
    {
        var template = scenario == "missing_template" || scenario == "template_not_file" ? null
            : scenario == "empty_template" ? [] : scenario == "invalid_template" ? new byte[] { 1, 2 } : Template();
        var data = scenario switch
        {
            "missing_data" => null,
            "null_data" => "null",
            "invalid_json" => "{",
            "invalid_data" => "{}",
            "invalid_binding" => "{\"tables\":[]}",
            _ => JsonSerializer.Serialize(Data())
        };
        using var form = Form(template, data);
        if (scenario == "duplicate_template") form.Add(new ByteArrayContent(Template()), "template", "second.xlsx");
        if (scenario == "duplicate_data") form.Add(new StringContent(data!), "data");
        if (scenario == "unknown_part") form.Add(new StringContent("unexpected"), "extra");
        if (scenario == "template_not_file") form.Add(new StringContent("not a file"), "template");
        await AssertError(await factory.CreateClient().PostAsync("/api/v1/print/excel/upload", form), 400, code, path);
    }

    [Theory]
    [InlineData("application/json", "{}", 415, "unsupported_media_type")]
    [InlineData("multipart/form-data", "", 400, "invalid_multipart")]
    [InlineData("multipart/form-data; boundary=broken", "not multipart", 400, "invalid_multipart")]
    public async Task UploadRejectsWrongOrMalformedEncoding(string contentType, string body, int status, string code)
    {
        using var content = new StringContent(body);
        content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
        await AssertError(await factory.CreateClient().PostAsync("/api/v1/print/pdf/upload", content), status, code);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("template")]
    [InlineData("expanded")]
    public async Task UploadHonorsExistingLimits(string limit)
    {
        using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<PrintApiOptions>(o =>
            {
                if (limit == "request") o.MaxRequestBytes = 32;
                if (limit == "template") o.MaxTemplateBytes = 32;
                if (limit == "expanded") o.MaxExpandedBytes = 32;
            })));
        using var form = Form(Template(), JsonSerializer.Serialize(Data()));
        await AssertError(await limited.CreateClient().PostAsync("/api/v1/print/excel/upload", form), 413, "limit_exceeded");
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }

    [Fact]
    public async Task ChunkedMultipartCannotBypassRequestLimit()
    {
        using var limited = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<PrintApiOptions>(o => o.MaxRequestBytes = 1024)));
        using var form = Form(Template(), JsonSerializer.Serialize(Data()));
        using var chunked = new UnknownLengthContent(await form.ReadAsByteArrayAsync());
        chunked.Headers.ContentType = form.Headers.ContentType;
        await AssertError(await limited.CreateClient().PostAsync("/api/v1/print/excel/upload", chunked), 413, "limit_exceeded", "request");
    }

    [Fact]
    public async Task SupportsDataBeforeFileAndJsonFilePart()
    {
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(Data())), "data", "data.json");
        form.Add(new ByteArrayContent(Template()), "template", "template.xlsx");
        var response = await factory.CreateClient().PostAsync("/api/v1/print/excel/upload", form);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task MixedConcurrentRequestsAreIndependent()
    {
        using var client = factory.CreateClient();
        var template = Template();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(async index =>
        {
            var value = "Request" + index;
            using var form = Form(template, JsonSerializer.Serialize(Data(value)));
            using var response = index % 2 == 0
                ? await client.PostAsync("/api/v1/print/excel/upload", form)
                : await client.PostAsJsonAsync("/api/v1/print/excel", new PrintRequest(new(Convert.ToBase64String(template)), Data(value)));
            response.EnsureSuccessStatusCode();
            using var workbook = new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
            Assert.Equal(value, workbook.Worksheet(1).Cell("A1").GetString());
        }));
    }

    [Fact]
    public async Task CancelledMultipartReaderLeavesRequestBodyOpen()
    {
        using var form = Form(Template(), JsonSerializer.Serialize(Data()));
        using var body = new MemoryStream(await form.ReadAsByteArrayAsync());
        var request = new Microsoft.AspNetCore.Http.DefaultHttpContext().Request;
        request.ContentType = form.Headers.ContentType!.ToString();
        request.Body = body;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MultipartPrintRequestReader.ReadAsync(request, new(), new(true)));
        Assert.True(body.CanRead);
    }

    [Fact]
    public async Task FailedAndCancelledUploadGenerationReleaseSharedSlot()
    {
        using var service = new PrintGenerationService(Options.Create(new PrintApiOptions { MaxConcurrency = 1 }));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GenerateAsync(Template(), Data(), false, new(true)));
        await Assert.ThrowsAsync<PrintValidationException>(() => service.GenerateAsync(new byte[] { 1, 2 }, Data(), false, CancellationToken.None));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var output = await service.GenerateAsync(new PrintRequest(new(Convert.ToBase64String(Template())), Data()), false, deadline.Token);
        using var workbook = new XLWorkbook(new MemoryStream(output));
        Assert.Equal("Upload", workbook.Worksheet(1).Cell("A1").GetString());
    }

    private sealed class FailingUploadService : IPrintGenerationService
    {
        public Task<byte[]> GenerateAsync(PrintRequest request, bool pdf, CancellationToken cancellationToken) => throw new InvalidOperationException("secret path");
        public Task<byte[]> GenerateAsync(byte[] template, PrintDataSet data, bool pdf, CancellationToken cancellationToken) => throw new InvalidOperationException("secret path");
    }

    [Fact]
    public async Task UploadGenerationFailureUsesSafeProblemResponse()
    {
        using var failing = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IPrintGenerationService, FailingUploadService>()));
        using var form = Form(Template(), JsonSerializer.Serialize(Data()));
        var response = await failing.CreateClient().PostAsync("/api/v1/print/pdf/upload", form);
        await AssertError(response, 500, "generation_failed");
        Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync());
    }
}
