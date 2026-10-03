using PrintSharp.Styles;

namespace PrintSharp.Layout.Fonts;

/// <summary>
/// 默认字体解析器实现。将逻辑字体规范映射到系统标准可用字体。
/// </summary>
public sealed class DefaultFontResolver : IFontResolver
{
    /// <summary>
    /// 获取全局默认字体解析器单例。
    /// </summary>
    public static DefaultFontResolver Instance { get; } = new();

    private readonly string _defaultFamily;

    /// <summary>
    /// 初始化 <see cref="DefaultFontResolver"/> 类的新实例。
    /// </summary>
    /// <param name="defaultFamily">默认字体系列名称，若未指定则默认为 "Segoe UI" 或 "Arial"。</param>
    public DefaultFontResolver(string defaultFamily = "Segoe UI")
    {
        _defaultFamily = defaultFamily;
    }

    /// <inheritdoc/>
    public ResolvedFont Resolve(FontSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        string family = string.IsNullOrWhiteSpace(spec.Family) ? _defaultFamily : spec.Family;
        return new ResolvedFont(spec, family);
    }
}
