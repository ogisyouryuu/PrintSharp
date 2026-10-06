namespace PrintSharp.Documents;

/// <summary>常用 ISO 纸张类型。</summary>
public enum PaperKind
{
    /// <summary>自定义尺寸；零尺寸随内容自适应。</summary>
    Customer,
    /// <summary>148 × 210 毫米。</summary>
    A5,
    /// <summary>176 × 250 毫米。</summary>
    B5,
    /// <summary>210 × 297 毫米。</summary>
    A4,
    /// <summary>250 × 353 毫米。</summary>
    B4,
    /// <summary>297 × 420 毫米。</summary>
    A3
}
