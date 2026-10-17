using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using PrintSharp.Api;
using PrintSharp.Pdf;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOptions<PrintApiOptions>().BindConfiguration("PrintApi").Validate(o => o.IsValid(), "Limits must be positive.").ValidateOnStart();
builder.WebHost.ConfigureKestrel((context, options) => options.Limits.MaxRequestBodySize =
    context.Configuration.GetValue<long?>("PrintApi:MaxRequestBytes") ?? new PrintApiOptions().MaxRequestBytes);
builder.Services.AddSingleton<IPrintGenerationService, PrintGenerationService>();
builder.Services.AddSingleton(NotoFontRegistry.Default);
var app = builder.Build();

app.Use(async (context, next) =>
{
    var watch = Stopwatch.StartNew();
    string? code = null;
    try
    {
        var limits = context.RequestServices.GetRequiredService<IOptions<PrintApiOptions>>().Value;
        if (context.Request.ContentLength > limits.MaxRequestBytes)
            throw new PrintValidationException("request", "limit_exceeded", "Request body exceeds the configured limit.", 413);
        var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (feature is { IsReadOnly: false })
            feature.MaxRequestBodySize = limits.MaxRequestBytes;
        await next(context);
    }
    catch (Exception exception)
    {
        var (status, error) = exception switch
        {
            PrintValidationException e => (e.Status, e.Error),
            BadHttpRequestException e => (e.StatusCode, new ValidationError("request", e.StatusCode == 413 ? "limit_exceeded" : "invalid_request", "Invalid request body.")),
            JsonException => (400, new ValidationError("request", "invalid_json", "Request is not valid JSON.")),
            OperationCanceledException => (context.RequestAborted.IsCancellationRequested ? 499 : 504, new ValidationError("request", "cancelled", "Request was cancelled or timed out.")),
            _ => (500, new ValidationError("request", "generation_failed", "Document generation failed."))
        };
        code = error.Code;
        if (status == 500)
            app.Logger.LogError("Generation failed. TraceId={TraceId} ExceptionType={ExceptionType}", context.TraceIdentifier, exception.GetType().Name);
        if (!context.RequestAborted.IsCancellationRequested && !context.Response.HasStarted)
        {
            context.Response.Clear();
            await Results.Problem(statusCode: status, title: "Print request failed", detail: error.Message,
                extensions: new Dictionary<string, object?> { ["errors"] = new[] { error }, ["traceId"] = context.TraceIdentifier })
                .ExecuteAsync(context);
        }
    }
    finally
    {
        app.Logger.LogInformation("Print request TraceId={TraceId} Route={Route} Status={Status} ErrorCode={ErrorCode} ElapsedMs={ElapsedMs}",
            context.TraceIdentifier, context.Request.Path, context.Response.StatusCode, code, watch.ElapsedMilliseconds);
    }
});
app.MapGet("/api/v1/health", () => Results.Ok(new { status = "ok" }));
app.MapPost("/api/v1/print/pdf", (HttpContext context, IPrintGenerationService service) => Generate(context, service, true));
app.MapPost("/api/v1/print/excel", (HttpContext context, IPrintGenerationService service) => Generate(context, service, false));
app.MapPost("/api/v1/print/pdf/upload", (HttpContext context, IPrintGenerationService service) => GenerateUpload(context, service, true));
app.MapPost("/api/v1/print/excel/upload", (HttpContext context, IPrintGenerationService service) => GenerateUpload(context, service, false));

async Task<IResult> GenerateUpload(HttpContext context, IPrintGenerationService service, bool pdf)
{
    var limits = context.RequestServices.GetRequiredService<IOptions<PrintApiOptions>>().Value;
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    timeout.CancelAfter(TimeSpan.FromSeconds(limits.ProcessingTimeoutSeconds));
    var (template, data) = await MultipartPrintRequestReader.ReadAsync(context.Request, limits, timeout.Token);
    var output = await service.GenerateAsync(template, data, pdf, timeout.Token);
    return GeneratedFile(context, data, output, pdf);
}

async Task<IResult> Generate(HttpContext context, IPrintGenerationService service, bool pdf)
{
    if (!context.Request.HasJsonContentType())
        throw new PrintValidationException("request", "unsupported_media_type", "Content-Type must be application/json.", 415);
    // Count actual bytes as well as Content-Length, including chunked requests and TestServer.
    var limits = context.RequestServices.GetRequiredService<IOptions<PrintApiOptions>>().Value;
    using var body = new MemoryStream();
    var buffer = new byte[8192];
    int count;
    while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted)) > 0)
    {
        PrintDataSetAdapter.Limit(body.Length + count, limits.MaxRequestBytes, "request");
        await body.WriteAsync(buffer.AsMemory(0, count), context.RequestAborted);
    }
    body.Position = 0;
    var request = await JsonSerializer.DeserializeAsync<PrintRequest>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web), context.RequestAborted);
    if (request is null)
        throw new PrintValidationException("request", "required", "Request is required.");
    var output = await service.GenerateAsync(request, pdf, context.RequestAborted);
    return GeneratedFile(context, request.Data, output, pdf);
}

IResult GeneratedFile(HttpContext context, PrintDataSet data, byte[] output, bool pdf)
{
    app.Logger.LogInformation("Generated TraceId={TraceId} Format={Format} Tables={Tables} Rows={Rows}", context.TraceIdentifier,
        pdf ? "pdf" : "xlsx", data.Tables.Count, data.Tables.Sum(t => (long)t.Rows.Count));
    return Results.File(output, pdf ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", pdf ? "document.pdf" : "document.xlsx");
}
app.Run();
public partial class Program;
