using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using PrintSharp.Pdf.Preview;

namespace PrintSharp.Preview.AspNetCore;

// キャッシュにはシリアライズ済みデータのみを保持し、ネイティブフォントの寿命から分離します。
public sealed class PreviewStore : IPrintPreviewStore, IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, PreviewEntry> _entries = new();
    private readonly PrintPreviewOptions _options;
    private readonly Timer _timer;
    private long _bytes;

    public PreviewStore(IOptions<PrintPreviewOptions> options)
    {
        _options = options.Value;
        _timer = new(_ => { lock (_gate) Sweep(); }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public PreviewEntry Add(PreviewDocument document, ClaimsPrincipal user, CancellationToken token)
    {
        string id = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        string revision = Guid.NewGuid().ToString("N");
        var expires = DateTimeOffset.UtcNow.AddSeconds(_options.TimeToLive.TotalSeconds);
        var pages = new List<byte[]>();
        long size = 0;
        foreach (var page in document.Pages)
        {
            token.ThrowIfCancellationRequested();
            var bytes = PreviewJson.SerializePage(document, page.PageNumber, id, revision);
            size += bytes.Length;
            CheckSize(size);
            pages.Add(bytes);
        }
        var pdf = document.ToPdfBytes(token);
        size += pdf.Length;
        CheckSize(size);
        token.ThrowIfCancellationRequested();
        var manifest = PreviewJson.SerializeManifest(document, id, revision, expires);
        size += manifest.Length;
        var textIndex = PreviewJson.SerializeTextIndex(document, id, revision);
        size += textIndex.Length;
        CheckSize(size);
        var entry = new PreviewEntry(id, revision, expires, _options.OwnerResolver(user), pages.ToArray(), pdf,
            manifest, size, textIndex);
        lock (_gate)
        {
            Sweep();
            if (_entries.Count >= _options.MaxSessions || _bytes + size > _options.MaxCacheBytes)
                throw new PreviewException("Preview cache is full. Retry later.", 503);
            _entries.Add(id, entry);
            _bytes += size;
        }
        return entry;
    }

    public PreviewEntry Get(string id, ClaimsPrincipal user)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(id, out var entry) || entry.Owner != _options.OwnerResolver(user))
                throw new PreviewException("Preview is missing or expired. Create it again.", 404);
            if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                Remove(id);
                throw new PreviewException("Preview expired. Create it again.", 410);
            }
            return entry;
        }
    }

    public PreviewSession Create(PrintSharp.Documents.Document document, ClaimsPrincipal owner, PrintSharp.Pdf.PdfRenderOptions? options = null, CancellationToken ct = default)
        => Create(PrintSharp.Pdf.SkiaPdfRenderer.Instance.CreatePreview(document, options, ct), owner, ct);
    public PreviewSession Create(PreviewDocument preview, ClaimsPrincipal owner, CancellationToken ct = default)
    {
        var entry = Add(preview, owner, ct);
        return new(entry.Id, entry.Pages.Length, entry.ExpiresAt);
    }
    public bool Delete(string id, ClaimsPrincipal user)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(id, out var entry) || entry.Owner != _options.OwnerResolver(user)) return false;
            Remove(id);
            return true;
        }
    }
    private void CheckSize(long size)
    {
        if (size > _options.MaxDocumentBytes) throw new PreviewException("Preview exceeds the configured size limit.", 413);
    }
    private void Sweep()
    {
        foreach (var id in _entries.Where(p => p.Value.ExpiresAt <= DateTimeOffset.UtcNow).Select(p => p.Key).ToArray()) Remove(id);
    }
    private void Remove(string id) { if (_entries.Remove(id, out var entry)) _bytes -= entry.Size; }
    public void Dispose() { _timer.Dispose(); lock (_gate) { _entries.Clear(); _bytes = 0; } }
}

public sealed record PreviewEntry(string Id, string Revision, DateTimeOffset ExpiresAt, string Owner, byte[][] Pages, byte[] Pdf, byte[] Manifest, long Size, byte[] TextIndex);
