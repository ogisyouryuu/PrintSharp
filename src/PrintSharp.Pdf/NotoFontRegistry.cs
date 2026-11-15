using System.Globalization;
using System.Text;
using SkiaSharp;
using Topten.RichTextKit;

namespace PrintSharp.Pdf;

/// <summary>同梱フォントを一度だけ読み込み、OS のフォント検索に依存しない組版を提供します。</summary>
public sealed class NotoFontRegistry : FontMapper, IDisposable
{
    private static readonly Lazy<NotoFontRegistry> Shared = new(() => new(Path.Combine(AppContext.BaseDirectory, "Fonts")));
    private readonly Dictionary<(string Family, bool Bold), SKTypeface> _faces = new();
    private readonly Dictionary<(string Family, bool Bold), SKFont> _fonts = new();
    private static readonly string[] Families = ["NotoSansCJKjp", "NotoSerifCJKjp", "NotoSansArabic", "NotoSans"];

    /// <summary>アプリケーション出力先の Fonts を共有します。</summary>
    public static new NotoFontRegistry Default => Shared.Value;

    /// <summary>指定したフォルダーから Noto フォントを読み込みます。</summary>
    public NotoFontRegistry(string directory) : this(directory, null) { }

    /// <summary>指定したフォントファミリーのみを読み込みます。</summary>
    public NotoFontRegistry(string directory, IEnumerable<string>? families)
    {
        try
        {
            foreach (var family in (families ?? Families).Distinct())
                foreach (var bold in new[] { false, true })
                {
                    string extension = family.Contains("CJK", StringComparison.Ordinal) ? "otf" : "ttf";
                    string path = Path.Combine(directory, $"{family}-{(bold ? "Bold" : "Regular")}.{extension}");
                    if (!File.Exists(path)) throw new FileNotFoundException("Bundled Noto font not found.", path);
                    var face = SKTypeface.FromFile(path) ?? throw new InvalidDataException($"Invalid font: {path}");
                    _faces.Add((family, bold), face);
                    _fonts.Add((family, bold), new(face));
                }
        }
        catch { Dispose(); throw; }
        if (_faces.Count == 0) throw new ArgumentException("At least one font family is required.", nameof(families));
    }

    /// <inheritdoc/>
    public override SKTypeface TypefaceFromStyle(IStyle style, bool ignoreFontVariants) =>
        _faces[(AvailableFamily(style.FontFamily), style.FontWeight >= 600)];

    /// <summary>論理フォント名を同梱フォントの名前に正規化します。</summary>
    public static string NormalizeFamily(string? family) => family?.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant() switch
    {
        "notoserifcjkjp" or "msmincho" or "ｍｓ明朝" or "yumincho" or "游明朝" or "timesnewroman" => "NotoSerifCJKjp",
        "notosansarabic" => "NotoSansArabic",
        "notosans" => "NotoSans",
        _ => "NotoSansCJKjp"
    };

    /// <summary>結合文字を分断せず、各書記素を実際のグリフ収録状況で解決します。</summary>
    public IEnumerable<(string Text, string Family)> ResolveRuns(string text, string? family, bool bold)
    {
        string preferred = AvailableFamily(family);
        string[] candidates = [preferred, .. _faces.Keys.Select(k => k.Family).Distinct().Where(f => f != preferred)];
        var elements = StringInfo.GetTextElementEnumerator(text);
        var run = new StringBuilder();
        string? current = null;
        while (elements.MoveNext())
        {
            string element = elements.GetTextElement();
            var selected = candidates.FirstOrDefault(candidate => element.EnumerateRunes().All(r =>
                IsLayoutControl(r) || GetFont(candidate, bold).GetGlyph(r.Value) != 0));
            if (selected is null)
                throw new InvalidOperationException($"Bundled Noto fonts do not cover text element: {element}");
            if (current != selected && run.Length > 0)
            {
                yield return (run.ToString(), current!);
                run.Clear();
            }
            current = selected;
            run.Append(element);
        }
        if (run.Length > 0) yield return (run.ToString(), current!);
    }

    private string AvailableFamily(string? family)
    {
        string normalized = NormalizeFamily(family);
        return _faces.ContainsKey((normalized, false)) ? normalized : _faces.Keys.First().Family;
    }

    private SKFont GetFont(string family, bool bold) => _fonts[(family, bold)];

    private static bool IsLayoutControl(Rune rune) => Rune.GetUnicodeCategory(rune) is
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;

    /// <summary>所有するネイティブフォントを解放します。</summary>
    public void Dispose()
    {
        foreach (var font in _fonts.Values) font.Dispose();
        _fonts.Clear();
        foreach (var face in _faces.Values) face.Dispose();
        _faces.Clear();
    }
}
