namespace PrintSharp.Layout;

/// <summary>
/// 表示二维空间中的逻辑物理矩形（由网格布局计算引擎根据 Row/Column 计算生成的结果，非第一层布局模型）。
/// </summary>
public readonly record struct LayoutRect
{
    /// <summary>
    /// 获取矩形左上角的 X 坐标（逻辑单位）。
    /// </summary>
    public float X { get; init; }

    /// <summary>
    /// 获取矩形左上角的 Y 坐标（逻辑单位）。
    /// </summary>
    public float Y { get; init; }

    /// <summary>
    /// 获取矩形的宽度（逻辑单位）。
    /// </summary>
    public float Width { get; init; }

    /// <summary>
    /// 获取矩形的高度（逻辑单位）。
    /// </summary>
    public float Height { get; init; }

    /// <summary>
    /// 获取矩形右边界的 X 坐标（X + Width）。
    /// </summary>
    public float Right => X + Width;

    /// <summary>
    /// 获取矩形下边界的 Y 坐标（Y + Height）。
    /// </summary>
    public float Bottom => Y + Height;

    /// <summary>
    /// 初始化 <see cref="LayoutRect"/> 结构体的新实例。
    /// </summary>
    public LayoutRect(float x, float y, float width, float height)
    {
        X = x;
        Y = y;
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
    }

    /// <summary>
    /// 获取空矩形。
    /// </summary>
    public static LayoutRect Empty => new(0, 0, 0, 0);

    /// <inheritdoc/>
    public override string ToString() => $"Rect(X={X:0.##}, Y={Y:0.##}, W={Width:0.##}, H={Height:0.##})";
}
