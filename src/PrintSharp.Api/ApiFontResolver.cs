using PdfSharp.Fonts;

namespace PrintSharp.Api;

// PDFsharp Core only resolves a small set of Windows families itself.
// A server-owned TTF provides a predictable fallback; no request can choose a file path.
public sealed class ApiFontResolver : IFontResolver
{
    private readonly byte[] _font;
    private ApiFontResolver(string path) => _font = File.ReadAllBytes(path);
    private static readonly object Gate = new();
    public static void Configure(string? path)
    {
        path ??= OperatingSystem.IsWindows() ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf") : null;
        if (path is null) return;
        lock (Gate)
        {
            if (GlobalFontSettings.FontResolver is null) GlobalFontSettings.FontResolver = new ApiFontResolver(path);
        }
    }
    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic) => new("api-fallback", bold, italic);
    public byte[] GetFont(string faceName) => _font;
}
