using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PrintSharp.Preview.AspNetCore;

// 圧縮した共有リソースとページのみを保持し、旧形式は要求時に復元します。
public sealed class PreviewEntry
{
    public string Id { get; }
    public string Revision { get; }
    public DateTimeOffset ExpiresAt { get; }
    public string Owner { get; }
    public byte[] Pdf { get; }
    public long Size { get; }
    public string FileName { get; init; } = "document.pdf";
    public int PageCount => _pages.Length;
    public byte[][] Pages => Enumerable.Range(1, PageCount).Select(n => Page(n, false)).ToArray();
    public byte[] Manifest => ManifestBytes(false);
    public byte[] TextIndex => Text(false);
    private readonly byte[][] _pages;
    private readonly byte[] _manifest;
    private readonly byte[] _text;
    private readonly Dictionary<string, byte[]> _resources = new();

    public PreviewEntry(string Id, string Revision, DateTimeOffset ExpiresAt, string Owner, byte[][] Pages,
        byte[] Pdf, byte[] Manifest, long Size, byte[] TextIndex)
    {
        this.Id = Id; this.Revision = Revision; this.ExpiresAt = ExpiresAt; this.Owner = Owner; this.Pdf = Pdf;
        _pages = Pages.Select(bytes =>
        {
            var node = JsonNode.Parse(bytes)!;
            var resources = node["resources"]!.AsObject();
            foreach (var pair in resources)
                if (!_resources.ContainsKey(pair.Key)) _resources.Add(pair.Key, Compress(JsonSerializer.SerializeToUtf8Bytes(pair.Value)));
            node["resourceIds"] = new JsonArray(resources.Select(p => (JsonNode?)JsonValue.Create(p.Key)).ToArray());
            node.AsObject().Remove("resources");
            node["page"]!["schemaVersion"] = 2;
            return Compress(JsonSerializer.SerializeToUtf8Bytes(node));
        }).ToArray();
        var manifest = JsonNode.Parse(Manifest)!;
        manifest["schemaVersion"] = 2;
        _manifest = Compress(JsonSerializer.SerializeToUtf8Bytes(manifest));
        var text = JsonNode.Parse(TextIndex)!;
        text["schemaVersion"] = 2;
        foreach (var page in text["pages"]!.AsArray())
            foreach (var region in page!["regions"]!.AsArray()) region!.AsObject().Remove("boxes");
        _text = Compress(JsonSerializer.SerializeToUtf8Bytes(text));
        this.Size = Pdf.LongLength + _manifest.Length + _text.Length + _pages.Sum(p => (long)p.Length) + _resources.Values.Sum(p => (long)p.Length);
    }

    public byte[] ManifestBytes(bool v2, bool gzip = false)
    {
        if (v2) return gzip ? _manifest : Expand(_manifest);
        var node = JsonNode.Parse(Expand(_manifest))!; node["schemaVersion"] = 1;
        return Encode(node, gzip);
    }
    public byte[] Page(int number, bool v2, bool gzip = false)
    {
        if (v2) return gzip ? _pages[number - 1] : Expand(_pages[number - 1]);
        var node = JsonNode.Parse(Expand(_pages[number - 1]))!;
        var ids = node["resourceIds"]!.AsArray().Select(id => id!.GetValue<string>());
        node["resources"] = ResourceObject(ids);
        node.AsObject().Remove("resourceIds"); node["page"]!["schemaVersion"] = 1;
        return Encode(node, gzip);
    }
    public byte[] Text(bool v2, bool gzip = false)
    {
        if (v2) return gzip ? _text : Expand(_text);
        var node = JsonNode.Parse(Expand(_text))!; node["schemaVersion"] = 1;
        for (int i = 0; i < PageCount; i++)
            node["pages"]![i]!["regions"] = JsonNode.Parse(Expand(_pages[i]))!["page"]!["textRegions"]!.DeepClone();
        return Encode(node, gzip);
    }
    public byte[] Resources(IEnumerable<string> ids, bool gzip = false) => Encode(new JsonObject
    {
        ["schemaVersion"] = 2, ["documentId"] = Id, ["revision"] = Revision, ["resources"] = ResourceObject(ids)
    }, gzip);
    private JsonObject ResourceObject(IEnumerable<string> ids)
    {
        var result = new JsonObject();
        foreach (string id in ids.Distinct())
        {
            if (!_resources.TryGetValue(id, out var bytes)) throw new PreviewException("Unknown resource.", 404);
            result[id] = JsonNode.Parse(Expand(bytes));
        }
        return result;
    }
    private static byte[] Encode(JsonNode node, bool gzip)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(node);
        return gzip ? Compress(bytes) : bytes;
    }
    internal static byte[] Compress(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, true)) gzip.Write(bytes);
        return output.ToArray();
    }
    private static byte[] Expand(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(); gzip.CopyTo(output); return output.ToArray();
    }
}
