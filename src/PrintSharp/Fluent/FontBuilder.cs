using PrintSharp.Styles;

namespace PrintSharp.Fluent;

/// <summary>
/// 逻辑字体配置 Fluent 构建器。
/// </summary>
public sealed class FontBuilder
{
    private string? _family;
    private float _size = 10f;
    private bool _bold;
    private bool _italic;
    private bool _underline;
    private bool _strikeThrough;

    /// <summary>
    /// 设置字体系列名称（例如 "Yu Gothic", "Arial"）。
    /// </summary>
    public FontBuilder Family(string? family)
    {
        _family = family;
        return this;
    }

    /// <summary>
    /// 设置逻辑字号大小（排版点数）。
    /// </summary>
    public FontBuilder Size(float size)
    {
        _size = size;
        return this;
    }

    /// <summary>
    /// 设置是否加粗。
    /// </summary>
    public FontBuilder Bold(bool bold = true)
    {
        _bold = bold;
        return this;
    }

    /// <summary>
    /// 设置是否斜体。
    /// </summary>
    public FontBuilder Italic(bool italic = true)
    {
        _italic = italic;
        return this;
    }

    /// <summary>
    /// 设置是否有下划线。
    /// </summary>
    public FontBuilder Underline(bool underline = true)
    {
        _underline = underline;
        return this;
    }

    /// <summary>
    /// 设置是否有删除线。
    /// </summary>
    public FontBuilder StrikeThrough(bool strike = true)
    {
        _strikeThrough = strike;
        return this;
    }

    /// <summary>
    /// 构建 <see cref="FontSpec"/> 实例。
    /// </summary>
    public FontSpec Build() => new(_family, _size, _bold, _italic, _underline, _strikeThrough);
}
