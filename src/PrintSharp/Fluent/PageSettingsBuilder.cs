using PrintSharp.Documents;
using PrintSharp.Styles;

namespace PrintSharp.Fluent;

/// <summary>
/// 页面设置 Fluent 构建器。
/// </summary>
public sealed class PageSettingsBuilder
{
    private PageOrientation _orientation = PageOrientation.Portrait;
    private float _width;
    private float _height;
    private PaperKind _paperKind = Documents.PaperKind.Customer;

    /// <summary>设置标准纸张或自定义纸张类型。</summary>
    public PageSettingsBuilder PaperKind(PaperKind paperKind)
    {
        if (!Enum.IsDefined(paperKind)) throw new ArgumentOutOfRangeException(nameof(paperKind));
        _paperKind = paperKind;
        return this;
    }
    private PaddingSpec _margins = new(20f);
    private float _defaultRowHeight = 20f;
    private float _defaultColumnWidth = 80f;

    /// <summary>
    /// 设置页面方向（纵向/横向）。
    /// </summary>
    public PageSettingsBuilder Orientation(PageOrientation orientation)
    {
        _orientation = orientation;
        return this;
    }

    /// <summary>
    /// 设置页面物理/逻辑尺寸。
    /// </summary>
    public PageSettingsBuilder Size(float width, float height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        _width = width;
        _height = height;
        _paperKind = Documents.PaperKind.Customer;
        return this;
    }

    /// <summary>
    /// 设置统一页边距。
    /// </summary>
    public PageSettingsBuilder Margins(float all)
    {
        _margins = new PaddingSpec(all);
        return this;
    }

    /// <summary>
    /// 设置四个方向的页边距。
    /// </summary>
    public PageSettingsBuilder Margins(float left, float top, float right, float bottom)
    {
        _margins = new PaddingSpec(left, top, right, bottom);
        return this;
    }

    /// <summary>
    /// 设置未指定高度行的默认行高。
    /// </summary>
    public PageSettingsBuilder DefaultRowHeight(float height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        _defaultRowHeight = height;
        return this;
    }

    /// <summary>
    /// 设置未指定宽度列的默认列宽。
    /// </summary>
    public PageSettingsBuilder DefaultColumnWidth(float width)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        _defaultColumnWidth = width;
        return this;
    }

    /// <summary>
    /// 构建 <see cref="PageSettings"/> 实例。
    /// </summary>
    public PageSettings Build()
    {
        return new PageSettings
        {
            Orientation = _orientation,
            PaperKind = _paperKind,
            Width = _width,
            Height = _height,
            Margins = _margins,
            DefaultRowHeight = _defaultRowHeight,
            DefaultColumnWidth = _defaultColumnWidth
        };
    }
}
