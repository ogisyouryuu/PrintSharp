namespace PrintSharp.Cells;

/// <summary>
/// 单元格内容类型。
/// </summary>
public enum CellType
{
    /// <summary>文本/基本数值类型（字符串、数值、日期等）。</summary>
    Text,

    /// <summary>图片类型（包含图像数据，如 byte[]、Stream、文件路径等）。</summary>
    Image,

    /// <summary>公式（由支持公式的渲染器如 Excel 计算或输出）。</summary>
    Formula,

    /// <summary>空白/占位单元格。</summary>
    Blank
}
