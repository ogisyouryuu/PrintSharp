namespace PrintSharp.Styles;

/// <summary>
/// 表示与特定渲染器无关的逻辑颜色规范（RGBA）。
/// </summary>
public readonly record struct ColorSpec
{
    /// <summary>
    /// 获取 Alpha 分量（0-255）。
    /// </summary>
    public byte A { get; init; }

    /// <summary>
    /// 获取 Red 分量（0-255）。
    /// </summary>
    public byte R { get; init; }

    /// <summary>
    /// 获取 Green 分量（0-255）。
    /// </summary>
    public byte G { get; init; }

    /// <summary>
    /// 获取 Blue 分量（0-255）。
    /// </summary>
    public byte B { get; init; }

    /// <summary>
    /// 初始化 <see cref="ColorSpec"/> 结构体的新实例。
    /// </summary>
    /// <param name="r">红色分量 (0-255)。</param>
    /// <param name="g">绿色分量 (0-255)。</param>
    /// <param name="b">蓝色分量 (0-255)。</param>
    /// <param name="a">不透明度分量 (0-255)，默认为 255（完全不透明）。</param>
    public ColorSpec(byte r, byte g, byte b, byte a = 255)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>
    /// 从 RGBA 数值创建颜色。
    /// </summary>
    public static ColorSpec FromRgba(byte r, byte g, byte b, byte a = 255) => new(r, g, b, a);

    /// <summary>
    /// 从 RGB 数值创建颜色（Alpha 默认为 255）。
    /// </summary>
    public static ColorSpec FromRgb(byte r, byte g, byte b) => new(r, g, b, 255);

    /// <summary>
    /// 从十六进制字符串（如 "#RRGGBB"、"#AARRGGBB" 或 "#RGB"）解析颜色。
    /// </summary>
    /// <param name="hex">十六进制颜色字符串。</param>
    /// <returns>解析后的 <see cref="ColorSpec"/>。</returns>
    /// <exception cref="ArgumentException">当十六进制字符串格式不合法时抛出。</exception>
    public static ColorSpec FromHex(string hex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hex);

        string cleanHex = hex.Trim().TrimStart('#');
        if (cleanHex.Length == 3)
        {
            // #RGB -> #RRGGBB
            byte r = Convert.ToByte(new string(cleanHex[0], 2), 16);
            byte g = Convert.ToByte(new string(cleanHex[1], 2), 16);
            byte b = Convert.ToByte(new string(cleanHex[2], 2), 16);
            return new ColorSpec(r, g, b, 255);
        }
        if (cleanHex.Length == 6)
        {
            // #RRGGBB
            byte r = Convert.ToByte(cleanHex.Substring(0, 2), 16);
            byte g = Convert.ToByte(cleanHex.Substring(2, 2), 16);
            byte b = Convert.ToByte(cleanHex.Substring(4, 2), 16);
            return new ColorSpec(r, g, b, 255);
        }
        if (cleanHex.Length == 8)
        {
            // #AARRGGBB
            byte a = Convert.ToByte(cleanHex.Substring(0, 2), 16);
            byte r = Convert.ToByte(cleanHex.Substring(2, 2), 16);
            byte g = Convert.ToByte(cleanHex.Substring(4, 2), 16);
            byte b = Convert.ToByte(cleanHex.Substring(6, 2), 16);
            return new ColorSpec(r, g, b, a);
        }

        throw new ArgumentException($"Invalid hex color string: '{hex}'", nameof(hex));
    }

    /// <summary>
    /// 转换为 "#AARRGGBB" 格式的十六进制字符串。
    /// </summary>
    public string ToHex(bool includeAlpha = true)
    {
        return includeAlpha
            ? $"#{A:X2}{R:X2}{G:X2}{B:X2}"
            : $"#{R:X2}{G:X2}{B:X2}";
    }

    /// <inheritdoc/>
    public override string ToString() => ToHex(A < 255);

    #region 常用颜色常量

    /// <summary>透明色 (0, 0, 0, 0)</summary>
    public static ColorSpec Transparent => new(0, 0, 0, 0);

    /// <summary>黑色</summary>
    public static ColorSpec Black => new(0, 0, 0);

    /// <summary>白色</summary>
    public static ColorSpec White => new(255, 255, 255);

    /// <summary>灰色</summary>
    public static ColorSpec Gray => new(128, 128, 128);

    /// <summary>浅灰色</summary>
    public static ColorSpec LightGray => new(211, 211, 211);

    /// <summary>暗灰色</summary>
    public static ColorSpec DarkGray => new(169, 169, 169);

    /// <summary>红色</summary>
    public static ColorSpec Red => new(255, 0, 0);

    /// <summary>绿色</summary>
    public static ColorSpec Green => new(0, 128, 0);

    /// <summary>蓝色</summary>
    public static ColorSpec Blue => new(0, 0, 255);

    /// <summary>黄色</summary>
    public static ColorSpec Yellow => new(255, 255, 0);

    /// <summary>橙色</summary>
    public static ColorSpec Orange => new(255, 165, 0);

    #endregion
}
