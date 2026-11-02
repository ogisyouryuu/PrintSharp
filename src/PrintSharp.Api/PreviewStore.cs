using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PrintSharp.Pdf.Preview;

namespace PrintSharp.Api;

// キャッシュにはシリアライズ済みデータのみを保持し、ネイティブフォントの寿命から分離します。
public sealed class PreviewStore : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private readonly object _gate = new();
    private readonly Dictionary<string, PreviewEntry> _entries = new();
    private readonly PrintApiOptions _options;
    private readonly Timer _timer;
    private long _bytes;

    public PreviewStore(IOptions<PrintApiOptions> options)
    {
        _options = options.Value;
        _timer = new(_ => { lock (_gate) Sweep(); }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public PreviewEntry Add(PreviewDocument document, ClaimsPrincipal user, CancellationToken token)
    {
        string id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        string revision = Guid.NewGuid().ToString("N");
        var expires = DateTimeOffset.UtcNow.AddSeconds(_options.PreviewTtlSeconds);
        var pages = new List<byte[]>();
        long size = 0;
        foreach (var page in document.Pages)
        {
            token.ThrowIfCancellationRequested();
            var ids = page.Commands.SelectMany(c => c.Glyphs?.Select(g => g.ResourceId) ?? (c.ResourceId is { } r ? new[] { r } : Array.Empty<string>())).Distinct();
            var resources = ids.ToDictionary(r => r, r => document.Resources[r]);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new { documentId = id, revision, page, resources }, Json);
            size += bytes.Length;
            CheckSize(size);
            pages.Add(bytes);
        }
        var pdf = document.ToPdfBytes(token);
        size += pdf.Length;
        CheckSize(size);
        token.ThrowIfCancellationRequested();
        var manifest = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, documentId = id, revision, expiresAt = expires,
            pageCount = pages.Count, pages = document.Pages.Select(p => new { p.PageNumber, p.Width, p.Height }) }, Json);
        size += manifest.Length;
        CheckSize(size);
        var entry = new PreviewEntry(id, revision, expires, Owner(user), pages.ToArray(), pdf,
            manifest, size);
        lock (_gate)
        {
            Sweep();
            if (_entries.Count >= _options.MaxPreviewSessions || _bytes + size > _options.MaxPreviewCacheBytes)
                throw new PrintValidationException("preview", "preview_capacity", "Preview cache is full. Retry later.", 503);
            _entries.Add(id, entry);
            _bytes += size;
        }
        return entry;
    }

    public PreviewEntry Get(string id, ClaimsPrincipal user)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(id, out var entry) || entry.Owner != Owner(user))
                throw new PrintValidationException("preview", "preview_unavailable", "Preview is missing or expired. Create it again.", 404);
            if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                Remove(id);
                throw new PrintValidationException("preview", "preview_expired", "Preview expired. Create it again.", 410);
            }
            return entry;
        }
    }

    public void Delete(string id, ClaimsPrincipal user)
    {
        lock (_gate) { Get(id, user); Remove(id); }
    }
    private void CheckSize(long size) => PrintDataSetAdapter.Limit(size, _options.MaxPreviewDocumentBytes, "preview");
    private static string Owner(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        ? string.Join("|", user.FindFirst("iss")?.Value, user.FindFirst("tid")?.Value, user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")?.Value ?? user.Identity.Name ?? throw new InvalidOperationException("Authenticated preview requires a stable user identifier."))
        : "anonymous";
    private void Sweep()
    {
        foreach (var id in _entries.Where(p => p.Value.ExpiresAt <= DateTimeOffset.UtcNow).Select(p => p.Key).ToArray()) Remove(id);
    }
    private void Remove(string id) { if (_entries.Remove(id, out var entry)) _bytes -= entry.Size; }
    public void Dispose() { _timer.Dispose(); lock (_gate) { _entries.Clear(); _bytes = 0; } }
}

public sealed record PreviewEntry(string Id, string Revision, DateTimeOffset ExpiresAt, string Owner, byte[][] Pages, byte[] Pdf, byte[] Manifest, long Size);
