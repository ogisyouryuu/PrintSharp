using System.Drawing.Printing;
using PrintSharp.Documents;

namespace PrintSharp.Windows;

/// <summary>为网格文档提供 Windows 打印 Fluent 扩展。</summary>
public static class PrintDocumentExtensions
{
    /// <summary>将文档转换为可配置的 <see cref="PrintDocument"/>；调用方负责释放返回对象。</summary>
    public static PrintDocument ToPrintDocument(this Document document, PrintDocumentRenderOptions? options = null)
        => PrintDocumentRenderer.Instance.RenderToPrintDocument(document, options);

    /// <summary>将文档发送到默认 Windows 打印机。</summary>
    public static void Print(this Document document, PrintDocumentRenderOptions? options = null)
        => PrintDocumentRenderer.Instance.Print(document, options);
}
