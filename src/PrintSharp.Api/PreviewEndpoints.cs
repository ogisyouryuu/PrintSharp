using System.Text.Json;
using Microsoft.Extensions.Options;

namespace PrintSharp.Api;

public static class PreviewEndpoints
{
    public static void MapPreviewEndpoints(this WebApplication app)
    {
        app.MapPost("/api/v1/previews", (HttpContext context, PrintGenerationService service, PreviewStore store) => Create(context, service, store, false));
        app.MapPost("/api/v1/previews/upload", (HttpContext context, PrintGenerationService service, PreviewStore store) => Create(context, service, store, true));
        app.MapGet("/api/v1/previews/{id}", (string id, HttpContext context, PreviewStore store) =>
        {
            var entry = store.Get(id, context.User);
            return Cached(context, entry, entry.Manifest, "manifest");
        });
        app.MapGet("/api/v1/previews/{id}/pages/{pageNumber:int}", (string id, int pageNumber, HttpContext context, PreviewStore store) =>
        {
            var entry = store.Get(id, context.User);
            if (pageNumber < 1 || pageNumber > entry.Pages.Length)
                throw new PrintValidationException("pageNumber", "page_out_of_range", "Page number is outside the document.", 404);
            return Cached(context, entry, entry.Pages[pageNumber - 1], $"page-{pageNumber}");
        });
        app.MapGet("/api/v1/previews/{id}/text", (string id, HttpContext context, PreviewStore store) =>
        {
            var entry = store.Get(id, context.User);
            return Cached(context, entry, entry.TextIndex, "text");
        });
        app.MapGet("/api/v1/previews/{id}/pdf", (string id, HttpContext context, PreviewStore store) =>
        {
            var entry = store.Get(id, context.User);
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.File(entry.Pdf, "application/pdf", "document.pdf");
        });
        app.MapDelete("/api/v1/previews/{id}", (string id, HttpContext context, PreviewStore store) =>
        {
            store.Delete(id, context.User);
            return Results.NoContent();
        });
    }

    private static IResult Cached(HttpContext context, PreviewEntry entry, byte[] bytes, string part)
    {
        string tag = $"\"{entry.Revision}-{part}\"";
        context.Response.Headers.ETag = tag;
        context.Response.Headers.CacheControl = "private, no-cache";
        if (context.Request.Headers.IfNoneMatch.Any(v => v?.Split(',').Any(t => t.Trim() == tag || t.Trim() == "W/" + tag || t.Trim() == "*") == true))
            return Results.StatusCode(304);
        return Results.Bytes(bytes, "application/json");
    }

    private static async Task<IResult> Create(HttpContext context, PrintGenerationService service, PreviewStore store, bool upload)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<PrintApiOptions>>().Value;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(options.ProcessingTimeoutSeconds));
        PreviewEntry entry;
        if (upload)
        {
            var (template, data) = await MultipartPrintRequestReader.ReadAsync(context.Request, options, timeout.Token);
            entry = await service.CreatePreviewAsync(template, data, document => store.Add(document, context.User, timeout.Token), timeout.Token);
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
            entry = await service.CreatePreviewAsync(request, document => store.Add(document, context.User, timeout.Token), timeout.Token);
        }
        context.Response.StatusCode = 201;
        context.Response.Headers.Location = $"/api/v1/previews/{entry.Id}";
        context.Response.Headers.CacheControl = "no-store";
        return Results.Bytes(entry.Manifest, "application/json");
    }
}
