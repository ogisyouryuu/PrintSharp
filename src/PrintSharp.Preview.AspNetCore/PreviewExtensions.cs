using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace PrintSharp.Preview.AspNetCore;
public static class PreviewExtensions
{
    public static IServiceCollection AddPrintSharpPreview(this IServiceCollection services, Action<PrintPreviewOptions>? configure = null)
    {
        var options = services.AddOptions<PrintPreviewOptions>();
        if (configure is not null) options.Configure(configure);
        options.Validate(o => o.TimeToLive > TimeSpan.Zero && o.MaxSessions > 0 && o.MaxCacheBytes > 0 && o.MaxDocumentBytes > 0 && o.OwnerResolver is not null, "Preview limits must be positive.").ValidateOnStart();
        services.TryAddSingleton<IPrintPreviewStore, PreviewStore>();
        return services;
    }
    public static RouteGroupBuilder MapPrintSharpPreview(this IEndpointRouteBuilder app, string prefix = "/api/previews")
    {
        var group = app.MapGroup(prefix);
        group.AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (PreviewException error) { return Results.Problem(statusCode: error.Status, detail: error.Message); }
        });
        group.MapGet("/{id}", (string id, HttpContext context, IPrintPreviewStore store) =>
        {
            var entry = store.Get(id, context.User);
            return Cached(context, entry, entry.Manifest, "manifest");
        });
        group.MapGet("/{id}/pages/{pageNumber:int}", (string id, int pageNumber, HttpContext context, IPrintPreviewStore store) =>
        {
            var entry = store.Get(id, context.User);
            if (pageNumber < 1 || pageNumber > entry.Pages.Length)
                throw new PreviewException( "Page number is outside the document.", 404);
            return Cached(context, entry, entry.Pages[pageNumber - 1], $"page-{pageNumber}");
        });
        group.MapGet("/{id}/text", (string id, HttpContext context, IPrintPreviewStore store) =>
        {
            var entry = store.Get(id, context.User);
            return Cached(context, entry, entry.TextIndex, "text");
        });
        group.MapGet("/{id}/pdf", (string id, HttpContext context, IPrintPreviewStore store) =>
        {
            var entry = store.Get(id, context.User);
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.File(entry.Pdf, "application/pdf", "document.pdf");
        });
        group.MapDelete("/{id}", (string id, HttpContext context, IPrintPreviewStore store) =>
        {
            store.Delete(id, context.User);
            return Results.NoContent();
        });
        return group;
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

}
