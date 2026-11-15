using System.Security.Claims;
using PrintSharp.Documents;
using PrintSharp.Pdf;
using PrintSharp.Pdf.Preview;
namespace PrintSharp.Preview.AspNetCore;
public sealed class PrintPreviewOptions
{
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromMinutes(10);
    public int MaxSessions { get; set; } = 100;
    public long MaxCacheBytes { get; set; } = 256 * 1024 * 1024;
    public long MaxDocumentBytes { get; set; } = 64 * 1024 * 1024;
    public Func<ClaimsPrincipal, string> OwnerResolver { get; set; } = user => user.Identity?.IsAuthenticated == true
        ? System.Text.Json.JsonSerializer.Serialize(new[] { user.FindFirst("iss")?.Value, user.FindFirst("tid")?.Value,
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value ?? user.Identity.Name
            ?? throw new InvalidOperationException("Authenticated preview requires a stable user identifier.") }) : "anonymous";
}
public interface IPrintPreviewStore
{
    PreviewSession Create(Document document, ClaimsPrincipal owner, PdfRenderOptions? options = null, CancellationToken ct = default);
    PreviewSession Create(PreviewDocument preview, ClaimsPrincipal owner, CancellationToken ct = default);
    PreviewEntry Get(string documentId, ClaimsPrincipal owner);
    bool Delete(string documentId, ClaimsPrincipal owner);
}
public sealed record PreviewSession(string DocumentId, int PageCount, DateTimeOffset ExpiresAt);
public sealed class PreviewException(string message, int status) : Exception(message)
{
    public int Status { get; } = status;
}
