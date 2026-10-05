using PdfSharp.Fonts;

namespace PrintSharp.WinForms.Demo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        GlobalFontSettings.FontResolver = new JapaneseFontResolver();
        ApplicationConfiguration.Initialize();
        if (args.Contains("--smoke-test"))
        {
            SmokeTest.Run(args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "SmokeOutput"));
            return;
        }
        Application.Run(new MainForm());
    }
}

// 日本語フォントを PDF に埋め込み、環境依存の文字化けを防ぐ。
internal sealed class JapaneseFontResolver : IFontResolver
{
    public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic)
        => new(bold ? "Japanese-Bold" : "Japanese-Regular", false, italic);

    public byte[] GetFont(string faceName)
    {
        string file = faceName == "Japanese-Bold" ? "meiryob.ttc" : "meiryo.ttc";
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), file);
        if (!File.Exists(path))
            throw new FileNotFoundException("PDF 导出需要 Windows 的 Meiryo 日文字体，请安装日语补充字体。", path);
        return TrueTypeCollection.ExtractFirstFont(File.ReadAllBytes(path));
    }
}

