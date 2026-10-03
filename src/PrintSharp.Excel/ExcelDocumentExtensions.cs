using ClosedXML.Excel;
using PrintSharp.Documents;

namespace PrintSharp.Excel;

/// <summary>
/// 为 <see cref="Document"/> 提供快捷导出 Excel 的扩展方法。
/// </summary>
public static class ExcelDocumentExtensions
{
    /// <summary>
    /// 将网格文档导出为 ClosedXML <see cref="XLWorkbook"/> 工作簿。
    /// </summary>
    public static XLWorkbook ToExcelWorkbook(this Document document, ExcelRenderOptions? options = null)
    {
        return ClosedXmlRenderer.Instance.RenderToWorkbook(document, options);
    }

    /// <summary>
    /// 将网格文档导出为 Excel 文件（.xlsx）。
    /// </summary>
    public static void SaveAsExcel(this Document document, string filePath, ExcelRenderOptions? options = null)
    {
        ClosedXmlRenderer.Instance.Render(document, filePath, options);
    }

    /// <summary>
    /// 将网格文档写入 Excel 数据流。
    /// </summary>
    public static void SaveAsExcel(this Document document, Stream stream, ExcelRenderOptions? options = null)
    {
        ClosedXmlRenderer.Instance.Render(document, stream, options);
    }

    /// <summary>
    /// 将网格文档转换为 Excel（.xlsx）字节数组。
    /// </summary>
    public static byte[] ToExcelBytes(this Document document, ExcelRenderOptions? options = null)
    {
        return ClosedXmlRenderer.Instance.RenderToBytes(document, options);
    }
}
