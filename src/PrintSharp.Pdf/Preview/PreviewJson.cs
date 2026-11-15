using System.Text.Json;
using System.Text.Json.Serialization;
namespace PrintSharp.Pdf.Preview;
/// <summary>Canvas プレビューのバージョン 1 JSON 契約を生成します。</summary>
public static class PreviewJson
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    /// <summary>バージョン 1 の応答を UTF-8 JSON として生成します。</summary>
    public static byte[] SerializeManifest(PreviewDocument preview, string documentId, string revision, DateTimeOffset expiresAt)
        => JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, documentId, revision, expiresAt, pageCount = preview.Pages.Count,
            pages = preview.Pages.Select(p => new { p.PageNumber, p.Width, p.Height }) }, Json);
    /// <summary>バージョン 1 の応答を UTF-8 JSON として生成します。</summary>
    public static byte[] SerializePage(PreviewDocument preview, int pageNumber, string documentId, string revision)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageNumber, preview.Pages.Count);
        var page = preview.Pages[pageNumber - 1];
        var ids = page.Commands.SelectMany(c => c.Glyphs?.Select(g => g.ResourceId) ?? (c.ResourceId is { } r ? new[] { r } : Array.Empty<string>())).Distinct();
        var resources = ids.ToDictionary(r => r, r => preview.Resources[r]);
        return JsonSerializer.SerializeToUtf8Bytes(new { documentId, revision, page, resources }, Json);
    }
    /// <summary>バージョン 1 の応答を UTF-8 JSON として生成します。</summary>
    public static byte[] SerializeTextIndex(PreviewDocument preview, string documentId, string revision)
        => JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, documentId, revision,
            pages = preview.Pages.Select(p => new { p.PageNumber, regions = p.TextRegions }) }, Json);
}
