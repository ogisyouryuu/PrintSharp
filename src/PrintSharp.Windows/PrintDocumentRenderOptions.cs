using PrintSharp.Layout.Fonts;

namespace PrintSharp.Windows;

/// <summary>Windows 打印输出选项。</summary>
public sealed record PrintDocumentRenderOptions
{
    /// <summary>每个 Core 逻辑尺寸单位对应的 GDI+ 点数。</summary>
    public float PointsPerLogicalUnit { get; init; } = 1f;

    /// <summary>字体解析器；未指定时使用默认解析器。</summary>
    public IFontResolver? FontResolver { get; init; }

    /// <summary>默认输出选项。</summary>
    public static PrintDocumentRenderOptions Default { get; } = new();
}
