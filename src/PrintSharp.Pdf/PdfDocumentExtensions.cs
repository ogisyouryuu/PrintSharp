using PrintSharp.Documents;

namespace PrintSharp.Pdf;

/// <summary>グリッド文書の PDF 出力拡張。</summary>
public static class PdfDocumentExtensions
{
    /// <summary>編集不可の PDF スナップショットを生成します。</summary>
    public static RenderedPdfDocument ToPdfDocument(this Document document, PdfRenderOptions? options = null)
        => SkiaPdfRenderer.Instance.RenderToDocument(document, options);

    /// <summary>将文档保存为 PDF 文件。</summary>
    public static void SaveAsPdf(this Document document, string filePath, PdfRenderOptions? options = null)
        => SkiaPdfRenderer.Instance.Render(document, filePath, options);

    /// <summary>将文档写入 PDF 数据流。</summary>
    public static void SaveAsPdf(this Document document, Stream stream, PdfRenderOptions? options = null)
        => SkiaPdfRenderer.Instance.Render(document, stream, options);

    /// <summary>将文档转换为 PDF 字节数组。</summary>
    public static byte[] ToPdfBytes(this Document document, PdfRenderOptions? options = null)
        => SkiaPdfRenderer.Instance.RenderToBytes(document, options);
}
