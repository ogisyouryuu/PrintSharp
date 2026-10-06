using PrintSharp.Styles;

namespace PrintSharp.Documents;

/// <summary>
/// 表示页面的逻辑布局与打印设置（逻辑尺寸，非渲染器特定设备单位）。
/// </summary>
public sealed record PageSettings
{
    /// <summary>纸张类型；默认使用自定义尺寸以兼容内容自适应布局。</summary>
    public PaperKind PaperKind { get; init; } = PaperKind.Customer;

    /// <summary>获取纵向纸张尺寸（逻辑单位，标准纸张按 72 点/英寸换算）。</summary>
    public (float Width, float Height) GetPaperSize()
    {
        var (width, height) = PaperKind switch
        {
            PaperKind.Customer => (Width, Height),
            PaperKind.A5 => (148f * 72f / 25.4f, 210f * 72f / 25.4f),
            PaperKind.B5 => (176f * 72f / 25.4f, 250f * 72f / 25.4f),
            PaperKind.A4 => (210f * 72f / 25.4f, 297f * 72f / 25.4f),
            PaperKind.B4 => (250f * 72f / 25.4f, 353f * 72f / 25.4f),
            PaperKind.A3 => (297f * 72f / 25.4f, 420f * 72f / 25.4f),
            _ => throw new ArgumentOutOfRangeException(nameof(PaperKind))
        };
        return (width, height);
    }

    /// <summary>
    /// 获取或设置页面方向（纵向/横向）。
    /// </summary>
    public PageOrientation Orientation { get; init; } = PageOrientation.Portrait;

    /// <summary>
    /// 获取或设置纸张逻辑宽度（单位：逻辑排版单位；若为 0 则由渲染器或纸张类型自适应）。
    /// </summary>
    public float Width { get; init; }

    /// <summary>
    /// 获取或设置纸张逻辑高度（单位：逻辑排版单位；若为 0 则由渲染器或纸张类型自适应）。
    /// </summary>
    public float Height { get; init; }

    /// <summary>
    /// 获取或设置页边距（逻辑单位）。
    /// </summary>
    public PaddingSpec Margins { get; init; } = new(20f);

    /// <summary>
    /// 获取或设置默认行高（当某行未显式指定高度时使用）。
    /// </summary>
    public float DefaultRowHeight { get; init; } = 20f;

    /// <summary>
    /// 获取或设置默认列宽（当某列未显式指定宽度时使用）。
    /// </summary>
    public float DefaultColumnWidth { get; init; } = 80f;

    /// <summary>
    /// 获取默认页面设置实例。
    /// </summary>
    public static PageSettings Default => new();
}
