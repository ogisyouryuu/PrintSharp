using System.Text;
using PrintSharp.Documents;

namespace PrintSharp.Html;

/// <summary>グリッド文書の HTML 出力拡張です。</summary>
public static class HtmlDocumentExtensions
{
    /// <summary>単独で表示・印刷できる HTML 文書を生成します。</summary>
    public static string ToHtml(this Document document)
        => HtmlRenderer.Render(document);

    /// <summary>文書を UTF-8 の HTML ファイルに保存します。</summary>
    public static void SaveAsHtml(this Document document, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        File.WriteAllText(filePath, document.ToHtml());
    }

    /// <summary>文書を UTF-8 で書き込み、ストリームを開いたままにします。</summary>
    public static void SaveAsHtml(this Document document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Write(Encoding.UTF8.GetBytes(document.ToHtml()));
    }
}
