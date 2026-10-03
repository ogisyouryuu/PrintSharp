using PdfSharp.Pdf;
using PrintSharp.Documents;

namespace PrintSharp.Pdf;

/// <summary>为网格文档提供 PDF 输出 Fluent 扩展。</summary>
public static class PdfDocumentExtensions
{
    /// <summary>将文档渲染为 PDFsharp 文档；调用方负责释放返回对象。</summary>
    public static PdfDocument ToPdfDocument(this Document document, PdfRenderOptions? options = null)
        => PdfSharpRenderer.Instance.RenderToDocument(document, options);

    /// <summary>将文档保存为 PDF 文件。</summary>
    public static void SaveAsPdf(this Document document, string filePath, PdfRenderOptions? options = null)
        => PdfSharpRenderer.Instance.Render(document, filePath, options);

    /// <summary>将文档写入 PDF 数据流。</summary>
    public static void SaveAsPdf(this Document document, Stream stream, PdfRenderOptions? options = null)
        => PdfSharpRenderer.Instance.Render(document, stream, options);

    /// <summary>将文档转换为 PDF 字节数组。</summary>
    public static byte[] ToPdfBytes(this Document document, PdfRenderOptions? options = null)
        => PdfSharpRenderer.Instance.RenderToBytes(document, options);
}
