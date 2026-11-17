using PrintSharp.Documents;

namespace PrintSharp.Word;

/// <summary>グリッド文書の Word 出力拡張です。</summary>
public static class WordDocumentExtensions
{
    /// <summary>編集可能な DOCX をバイト配列として生成します。</summary>
    public static byte[] ToDocxBytes(this Document document)
        => WordRenderer.Render(document);

    /// <summary>編集可能な Word 文書を保存します。</summary>
    public static void SaveAsDocx(this Document document, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        File.WriteAllBytes(filePath, document.ToDocxBytes());
    }

    /// <summary>DOCX を書き込み、出力ストリームを開いたままにします。</summary>
    public static void SaveAsDocx(this Document document, Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        stream.Write(document.ToDocxBytes());
    }
}
