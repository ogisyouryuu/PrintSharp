namespace PrintSharp.Pdf;

/// <summary>PDF 输出尺寸与字体解析选项。</summary>
public sealed record PdfRenderOptions
{
    /// <summary>每个 Core 逻辑尺寸单位对应的 PDF 点数。</summary>
    public float PointsPerLogicalUnit { get; init; } = 1f;

    /// <summary>Core 字体解析器；未指定时使用默认解析器。</summary>
    public PrintSharp.Layout.Fonts.IFontResolver? FontResolver { get; init; }

    /// <summary>默认 PDF 渲染选项。</summary>
    public static PdfRenderOptions Default { get; } = new();
}
