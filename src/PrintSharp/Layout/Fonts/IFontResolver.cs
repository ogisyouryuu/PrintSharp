using PrintSharp.Styles;

namespace PrintSharp.Layout.Fonts;

/// <summary>
/// 逻辑字体解析接口。负责将跨平台的逻辑 <see cref="FontSpec"/> 解析为具体运行环境可用的字体资源。
/// </summary>
public interface IFontResolver
{
    /// <summary>
    /// 解析逻辑字体规范。
    /// </summary>
    /// <param name="spec">待解析的逻辑字体规范。</param>
    /// <returns>已解析的字体描述。</returns>
    ResolvedFont Resolve(FontSpec spec);
}
