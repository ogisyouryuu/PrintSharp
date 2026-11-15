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
            return Cached(context, entry, entry.ManifestBytes(V2(context), Gzip(context)), "manifest");
        });
        group.MapGet("/{id}/pages/{pageNumber:int}", (string id, int pageNumber, HttpContext context, IPrintPreviewStore store) =>
        {
            var entry = store.Get(id, context.User);
            if (pageNumber < 1 || pageNumber > entry.PageCount)
                throw new PreviewException( "Page number is outside the document.", 404);
            return Cached(context, entry, entry.Page(pageNumber, V2(context), Gzip(context)), $"page-{pageNumber}");
        });
        group.MapGet("/{id}/text", (string id, HttpContext context, IPrintPreviewStore store) =>
        {
            var entry = store.Get(id, context.User);
            return Cached(context, entry, entry.Text(V2(context), Gzip(context)), "text");
        });
        group.MapGet("/{id}/pdf", (string id, HttpContext context, IPrintPreviewStore store) =>
        {
            var entry = store.Get(id, context.User);
            context.Response.Headers.CacheControl = "private, no-store";
            return Results.File(entry.Pdf, "application/pdf", PdfFileName.Sanitize(context.Request.Query["filename"].FirstOrDefault() ?? entry.FileName));
        });
        group.MapGet("/{id}/resources", (string id, HttpContext context, IPrintPreviewStore store) =>
        {
            var entry = store.Get(id, context.User);
            var ids = context.Request.Query["ids"].ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
            if (ids.Length is 0 or > 128) throw new PreviewException("Request between 1 and 128 resource IDs.", 400);
            string part = "resources-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join(',', ids))));
            return Cached(context, entry, entry.Resources(ids, Gzip(context)), part);
        });
        group.MapDelete("/{id}", (string id, HttpContext context, IPrintPreviewStore store) =>
        {
            store.Delete(id, context.User);
            return Results.NoContent();
        });
        return group;
    }

    private static bool V2(HttpContext context) => context.Request.Query["schemaVersion"] == "2";
    private static bool Gzip(HttpContext context)
    {
        var encodings = context.Request.GetTypedHeaders().AcceptEncoding;
        var explicitGzip = encodings?.FirstOrDefault(e => e.Value.Equals("gzip", StringComparison.OrdinalIgnoreCase));
        return explicitGzip is not null ? (explicitGzip.Quality ?? 1) > 0
            : encodings?.Any(e => e.Value == "*" && (e.Quality ?? 1) > 0) == true;
    }

    private static IResult Cached(HttpContext context, PreviewEntry entry, byte[] bytes, string part)
    {
        context.Response.Headers.Vary = "Accept-Encoding";
        bool gzip = Gzip(context);
        if (gzip) context.Response.Headers.ContentEncoding = "gzip";
        if (V2(context)) part += "-v2";
        if (gzip) part += "-gzip";
        string tag = $"\"{entry.Revision}-{part}\"";
        context.Response.Headers.ETag = tag;
        context.Response.Headers.CacheControl = "private, no-cache";
        if (context.Request.Headers.IfNoneMatch.Any(v => v?.Split(',').Any(t => t.Trim() == tag || t.Trim() == "W/" + tag || t.Trim() == "*") == true))
            return Results.StatusCode(304);
        return Results.Bytes(bytes, "application/json");
    }

}
