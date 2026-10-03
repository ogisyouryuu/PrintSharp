using PrintSharp.Styles;

namespace PrintSharp.Layout.Fonts;

/// <summary>
/// 表示字体解析器解析后的具体字体资源描述。
/// </summary>
public sealed class ResolvedFont
{
    /// <summary>
    /// 获取原始逻辑字体规范。
    /// </summary>
    public FontSpec Spec { get; }

    /// <summary>
    /// 获取已解析的目标字体系列名称。
    /// </summary>
    public string ResolvedFamilyName { get; }

    /// <summary>
    /// 获取字体文件绝对路径（若来自本地文件）。
    /// </summary>
    public string? FontPath { get; }

    /// <summary>
    /// 获取字体二进制字节数据（若嵌入或从内存加载）。
    /// </summary>
    public byte[]? FontData { get; }

    /// <summary>
    /// 初始化 <see cref="ResolvedFont"/> 类的新实例。
    /// </summary>
    public ResolvedFont(
        FontSpec spec,
        string resolvedFamilyName,
        string? fontPath = null,
        byte[]? fontData = null)
    {
        Spec = spec;
        ResolvedFamilyName = resolvedFamilyName;
        FontPath = fontPath;
        FontData = fontData;
    }
}
