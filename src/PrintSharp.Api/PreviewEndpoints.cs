using PrintSharp.Preview.AspNetCore;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PrintSharp.Api;

public static class PreviewEndpoints
{
    public static void MapPreviewEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/previews", (HttpContext context, PrintGenerationService service, IPrintPreviewStore store) => Create(context, service, store, false));
        app.MapPost("/api/v1/previews/upload", (HttpContext context, PrintGenerationService service, IPrintPreviewStore store) => Create(context, service, store, true));
        app.MapPrintSharpPreview("/api/v1/previews");
    }

    private static async Task<IResult> Create(HttpContext context, PrintGenerationService service, IPrintPreviewStore store, bool upload)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<PrintApiOptions>>().Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.ProcessingTimeoutSeconds));
        PreviewEntry entry;
        if (upload)
        {
            var (template, data) = await MultipartPrintRequestReader.ReadAsync(context.Request, options, timeout.Token);
            entry = await service.CreatePreviewAsync(template, data, document => { var session = store.Create(document, context.User, timeout.Token); return store.Get(session.DocumentId, context.User); }, timeout.Token);
        }
        else
        {
            if (!context.Request.HasJsonContentType())
                throw new PrintValidationException("request", "unsupported_media_type", "Content-Type must be application/json.", 415);
            using var body = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await context.Request.Body.ReadAsync(buffer, timeout.Token)) > 0)
            {
                PrintDataSetAdapter.Limit(body.Length + count, options.MaxRequestBytes, "request");
                body.Write(buffer, 0, count);
            }
            body.Position = 0;
            var request = await JsonSerializer.DeserializeAsync<PrintRequest>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web), timeout.Token)
                ?? throw new PrintValidationException("request", "required", "Request is required.");
            entry = await service.CreatePreviewAsync(request, document => { var session = store.Create(document, context.User, timeout.Token); return store.Get(session.DocumentId, context.User); }, timeout.Token);
        }
        context.Response.StatusCode = 201;
        context.Response.Headers.Location = $"/api/v1/previews/{entry.Id}";
        context.Response.Headers.CacheControl = "no-store";
        return Results.Bytes(entry.Manifest, "application/json");
    }
}
