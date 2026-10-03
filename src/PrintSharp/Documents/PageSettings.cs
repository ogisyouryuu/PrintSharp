using PrintSharp.Styles;

namespace PrintSharp.Documents;

/// <summary>
/// 表示页面的逻辑布局与打印设置（逻辑尺寸，非渲染器特定设备单位）。
/// </summary>
public sealed record PageSettings
{
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
